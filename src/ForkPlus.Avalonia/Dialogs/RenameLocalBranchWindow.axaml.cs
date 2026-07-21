using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.x：Avalonia 版 RenameLocalBranchWindow（真实迁移版，对照 WPF RenameLocalBranchWindow.xaml.cs 234 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/RenameLocalBranchWindow.xaml.cs：
    //   - public partial class RenameLocalBranchWindow : ForkPlusDialogWindow
    //   - 字段: GitModule _gitModule / RepositoryReferences _references / LocalBranch _localBranch / RemoteBranch _remoteBranch
    //   - 构造函数 (GitModule, RepositoryReferences, LocalBranch, [Null] string newName)
    //   - IsSubmitAllowed: 名字空 false / rename remote 重复 Warning / 名字未变 false / Validate 失败 Warning / 重复 Warning
    //   - GetCommandPreview: "git branch -m {name} {newName}" + 可选 "git push ..." 远端重命名
    //   - OnSubmit: RenameLocalBranchGitCommand + 可选 RenameRemoteBranchGitCommand + UpdateTrackingReferenceGitCommand
    //     → 更新 Pinned/Filter references → Close(result)
    //
    // Avalonia 版差异（spike）：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 依赖 → 注入 Action<GitCommandResult>? onCompleted 回调
    //   3. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   4. spike 基类不提供 DisableEditableControls → 手动禁用 BranchNameTextBox + RenameRemoteBranchCheckbox
    //   5. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   6. PlaceholderTextBox.Placeholder → TextBox Watermark
    //   7. ReferenceTextBox + ReferenceNameAutocompleteProvider → spike 版用普通 TextBox（autocomplete 暂不接入）
    //   8. CheckBox Checked/Unchecked 事件 → IsCheckedChanged 事件
    //   9. TextChangedEventArgs → Avalonia 同名类型
    public partial class RenameLocalBranchWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly RepositoryReferences _references;
        private readonly LocalBranch _localBranch;
        private readonly RemoteBranch _remoteBranch;
        private readonly Action<GitCommandResult>? _onCompleted;

        // 构造函数签名与 WPF 不同：用 Action<GitCommandResult>? onCompleted 回调替代 RepositoryUserControl 依赖
        public RenameLocalBranchWindow(
            GitModule gitModule,
            RepositoryReferences references,
            LocalBranch localBranch,
            string newName = null,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _references = references ?? throw new ArgumentNullException(nameof(references));
            _localBranch = localBranch ?? throw new ArgumentNullException(nameof(localBranch));
            _onCompleted = onCompleted;

            // 对照 WPF: 查找 localBranch 对应的 upstream remote branch
            // spike: IReadOnlyListExtensions.FirstItem 替代 LINQ
            RemoteBranch remoteBranch = IReadOnlyListExtensions.FirstItem(
                _references.RemoteBranches,
                (RemoteBranch x) => x.FullReference == localBranch.UpstreamFullReference);

            if (remoteBranch != null && remoteBranch.ShortName == _localBranch.Name)
            {
                _remoteBranch = remoteBranch;
                // 对照 WPF: RenameRemoteBranchCheckbox.Show()
                RenameRemoteBranchCheckbox.IsVisible = true;
                // 对照 WPF: Content = string.Format(Translate("Also rename {0}"), remoteBranch.Name.Replace("_", "__"));
                // spike: 不做 "_" → "__" 转义（Avalonia CheckBox Content 不解析 _ 为快捷键前缀）
                RenameRemoteBranchCheckbox.Content = string.Format(Translate("Also rename {0}"), remoteBranch.Name);
            }
            else
            {
                // 对照 WPF: RenameRemoteBranchCheckbox.Collapse()
                RenameRemoteBranchCheckbox.IsVisible = false;
            }

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Rename Local Branch");
            DialogDescription = Translate("Rename local branch");
            SubmitButtonTitle = Translate("Rename");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Rename Local Branch");

            // 对照 WPF: BranchNameTextBox.Text = newName ?? localBranch.Name;
            BranchNameTextBox.Text = newName ?? localBranch.Name;
            // 对照 WPF: BranchNameTextBox.SelectAll();
            BranchNameTextBox.SelectionStart = 0;
            BranchNameTextBox.SelectionEnd = BranchNameTextBox.Text?.Length ?? 0;

            // spike: 跳过 ReferenceTextBox.SetAutocompleteProvider（autocomplete 暂不接入）

            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                SetStatus(ForkPlusDialogStatus.None, string.Empty);
                string newName = (BranchNameTextBox.Text ?? "").ToLower();
                if (string.IsNullOrEmpty(newName))
                {
                    return false;
                }
                if (RenameRemoteBranchCheckbox.IsChecked.GetValueOrDefault())
                {
                    SetStatus(ForkPlusDialogStatus.Warning, Translate("Renaming can break tracking references for other users"));
                    string upstreamRemote = UpstreamRemote(_localBranch);
                    RemoteBranch remoteBranch = IReadOnlyListExtensions.FirstItem(
                        _references.RemoteBranches,
                        (RemoteBranch x) => x.ShortName.ToLower() == newName && x.Remote == upstreamRemote);
                    if (remoteBranch != null)
                    {
                        SetStatus(ForkPlusDialogStatus.Warning, string.Format(Translate("Branch {0} already exists"), remoteBranch.Name));
                        return false;
                    }
                }
                if (newName == _localBranch.Name.ToLower())
                {
                    return false;
                }
                string error = ReferenceNameValidator.Validate(newName);
                if (error != null)
                {
                    SetStatus(ForkPlusDialogStatus.Warning, error);
                    return false;
                }
                if (_references.LocalBranches.AnyItem((LocalBranch x) => x.Name.ToLower() == newName))
                {
                    SetStatus(ForkPlusDialogStatus.Warning, string.Format(Translate("Branch '{0}' already exists"), BranchNameTextBox.Text));
                    return false;
                }
                return true;
            }
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string GetCommandPreview()
        {
            string newName = BranchNameTextBox.Text ?? "";
            if (string.IsNullOrWhiteSpace(newName))
            {
                return null;
            }
            string command = "git branch -m " + _localBranch.Name + " " + newName;
            if (RenameRemoteBranchCheckbox.IsChecked.GetValueOrDefault() && _remoteBranch != null)
            {
                command += "\ngit push " + _remoteBranch.Remote + " " + newName + " :" + _remoteBranch.ShortName;
            }
            return command;
        }

        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBox == null) return;
            string preview = GetCommandPreview();
            CommandPreviewTextBox.Text = preview ?? string.Empty;
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            GitModule gitModule = _gitModule;
            LocalBranch localBranch = _localBranch;
            RemoteBranch remoteBranch = _remoteBranch;
            string newName = BranchNameTextBox.Text ?? "";
            string[] pinned = _references.PinnedReferences;
            string[] filtered = _references.FilterReferences;
            bool renameUpstream = RenameRemoteBranchCheckbox.IsChecked.GetValueOrDefault();

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Renaming branch..."));

            // 对照 WPF: MainWindow.ActiveRepositoryUserControl.JobQueue.Add(FormatTranslate("Rename branch '{0}'", ...), ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor（spike 版不做 undo）
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult renameResult = new RenameLocalBranchGitCommand().Execute(gitModule, localBranch.Name, newName, monitor);
                if (!renameResult.Succeeded)
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        try { _onCompleted?.Invoke(renameResult); } catch (Exception ex) { Log.Error("RenameLocalBranchWindow onCompleted callback failed", ex); }
                        Close(renameResult);
                    });
                    return;
                }

                // 对照 WPF: 构造新的 LocalBranch 引用并更新 Pinned/Filter references
                string fullReference = "refs/heads/" + newName;
                LocalBranch newLocalBranch = new LocalBranch(localBranch.Sha, fullReference, newName, false, null, DateTime.Now);
                int pinnedIdx = Array.IndexOf(pinned, localBranch.FullReference);
                if (pinnedIdx != -1) pinned[pinnedIdx] = newLocalBranch.FullReference;
                int filteredIdx = Array.IndexOf(filtered, localBranch.FullReference);
                if (filteredIdx != -1) filtered[filteredIdx] = newLocalBranch.FullReference;

                if (renameUpstream && remoteBranch != null)
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        SetStatus(ForkPlusDialogStatus.InProgress, Translate("Renaming remote branch..."));
                    });
                    GitCommandResult renameRemoteResult = new RenameRemoteBranchGitCommand().Execute(gitModule, remoteBranch, newName, monitor);
                    if (!renameRemoteResult.Succeeded)
                    {
                        Dispatcher.UIThread.Post(delegate
                        {
                            try { _onCompleted?.Invoke(renameRemoteResult); } catch (Exception ex) { Log.Error("RenameLocalBranchWindow onCompleted callback failed", ex); }
                            Close(renameRemoteResult);
                        });
                        return;
                    }

                    Dispatcher.UIThread.Post(delegate
                    {
                        SetStatus(ForkPlusDialogStatus.InProgress, Translate("Updating tracking reference..."));
                    });
                    string newRemoteFullRef = "refs/remotes/" + remoteBranch.Remote + "/" + newName;
                    string newRemoteFullName = remoteBranch.Remote + "/" + newName;
                    RemoteBranch newRemoteBranch = new RemoteBranch(remoteBranch.Sha, newRemoteFullRef, newRemoteFullName, newName, remoteBranch.Remote, remoteBranch.CommitterDate);
                    GitCommandResult updateTrackingResult = new UpdateTrackingReferenceGitCommand().Execute(gitModule, newLocalBranch, newRemoteBranch, monitor);
                    if (!updateTrackingResult.Succeeded)
                    {
                        Dispatcher.UIThread.Post(delegate
                        {
                            try { _onCompleted?.Invoke(updateTrackingResult); } catch (Exception ex) { Log.Error("RenameLocalBranchWindow onCompleted callback failed", ex); }
                            Close(updateTrackingResult);
                        });
                        return;
                    }
                    int pinnedRemoteIdx = Array.IndexOf(pinned, remoteBranch.FullReference);
                    if (pinnedRemoteIdx != -1) pinned[pinnedRemoteIdx] = newRemoteBranch.FullReference;
                    int filteredRemoteIdx = Array.IndexOf(filtered, remoteBranch.FullReference);
                    if (filteredRemoteIdx != -1) filtered[filteredRemoteIdx] = newRemoteBranch.FullReference;
                }

                // 对照 WPF: gitModule.Settings.PinnedReferences = pinned; gitModule.Settings.FilterReferences = filtered; gitModule.Settings.Save();
                try
                {
                    gitModule.Settings.PinnedReferences = pinned;
                    gitModule.Settings.FilterReferences = filtered;
                    gitModule.Settings.Save();
                }
                catch (Exception ex)
                {
                    Log.Error("RenameLocalBranchWindow: failed to save repository settings", ex);
                }

                Dispatcher.UIThread.Post(delegate
                {
                    try { _onCompleted?.Invoke(renameResult); } catch (Exception ex) { Log.Error("RenameLocalBranchWindow onCompleted callback failed", ex); }
                    Close(renameResult);
                });
            });
        }

        // 对照 WPF: BranchName_TextChanged
        public void BranchName_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: RenameRemoteBranchCheckbox_Changed（WPF 用 Checked/Unchecked 两个事件，Avalonia 用 IsCheckedChanged）
        public void RenameRemoteBranchCheckbox_Changed(object? sender, RoutedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: private static string UpstreamRemote(LocalBranch localBranch)
        private static string UpstreamRemote(LocalBranch localBranch)
        {
            string upstreamFullName = localBranch.UpstreamFullName;
            if (upstreamFullName == null)
            {
                return null;
            }
            int num = upstreamFullName.IndexOf("/");
            if (num == -1)
            {
                return null;
            }
            return upstreamFullName.Substring(0, num);
        }

        // spike 版：手动禁用可编辑控件（基类不提供 DisableEditableControls）
        private void DisableEditableControls()
        {
            BranchNameTextBox.IsEnabled = false;
            RenameRemoteBranchCheckbox.IsEnabled = false;
        }

        // 对照 WPF: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
        private static string Translate(string text)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.Translate(text, userSettings.UiLanguage);
            }
            return text;
        }
    }
}
