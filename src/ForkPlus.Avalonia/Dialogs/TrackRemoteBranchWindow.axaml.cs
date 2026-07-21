using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.x：Avalonia 版 TrackRemoteBranchWindow（真实迁移版，对照 WPF TrackRemoteBranchWindow.xaml.cs 317 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/TrackRemoteBranchWindow.xaml.cs：
    //   - public partial class TrackRemoteBranchWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / LocalBranch[] _localBranches / RemoteBranch _remoteBranch
    //   - 构造函数 (RepositoryUserControl, LocalBranch[], RemoteBranch):
    //     * DialogTitle="Track Remote Branch" / DialogDescription="Create new local branch which tracks remote branch"
    //     * SubmitButtonTitle="Track"
    //     * GitPointView.Value = remoteBranch
    //     * LocalBranchNameTextBox.Text = remoteBranch.ShortName
    //     * 根据 RepositoryStatus.WorkingDirectoryIsDirty 决定是否显示 LocalChanges 区块
    //     * StashAndReapplyRadioButton.IsChecked = ForkPlusSettings.Default.Checkout_StashAndReapply
    //     * KeyDown/KeyUp 监听 Shift → 切换 "Stash and reapply" / "Leave as stash"
    //   - IsSubmitAllowed override: 名字空 false / Validate 失败 Warning / 重复分支 Warning
    //   - GetCommandPreview override: "git checkout [--force] -b <localName> <remoteBranch.Name>"
    //   - OnSubmit: JobQueue.Add → 解析 StashAndReapply / Discard → PerformTrackBranch
    //     * leaveAsStash: SaveStashGitCommand（不 reapply）
    //     * stashAndReapply == Required: SaveStashGitCommand → CreateLocalAndTrackRemoteBranchGitCommand → ApplyStashGitCommand
    //     * stashAndReapply == Possible: 先直接 CreateLocalAndTrackRemoteBranchGitCommand，
    //       若 CheckoutLocalChangesWouldBeOverwritten 自动重试 Required 路径
    //     * 结尾 UpdateSubmodulesGitCommand
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + RepositoryReferences + LocalBranch[] + RemoteBranch + Action 回调
    //      + bool workingDirectoryIsDirty + SubmodulesToUpdate（默认 empty）
    //   3. GitPointView → TextBlock 显示 remoteBranch.Name（spike 简化）
    //   4. ReferenceTextBox → 普通 TextBox + Watermark
    //   5. StashAndReapply 枚举（WPF ForkPlus.UI.Dialogs）→ spike 版定义本地枚举
    //   6. KeyboardHelper.IsShiftDown shift 切换逻辑 → spike 版省略（"Leave as stash" UX 不迁移）
    //   7. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   8. spike 基类不提供 DisableEditableControls → 手动禁用 LocalBranchNameTextBox + RadioButtons
    //   9. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //  10. _localBranches.AnyItem → LINQ Any
    public partial class TrackRemoteBranchWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // spike 版本地 StashAndReapply 枚举（对照 WPF ForkPlus.UI.Dialogs.StashAndReapply）。
        // WPF 端 StashAndReapply 在 ForkPlus.UI.Dialogs 命名空间，Avalonia 工程不可访问；
        // spike 阶段定义本地枚举保持 OnSubmit/PerformTrackBranch 逻辑结构。
        private enum StashAndReapply
        {
            Possible,
            Required,
            Forbidden
        }

        private readonly GitModule _gitModule;
        private readonly RepositoryReferences? _references;
        private readonly LocalBranch[] _localBranches;
        private readonly RemoteBranch _remoteBranch;
        private readonly bool _workingDirectoryIsDirty;
        private readonly SubmodulesToUpdate _submodulesToUpdate;
        private readonly Action<GitCommandResult>? _onCompleted;

        // 构造函数签名与 WPF 不同：用 GitModule + RepositoryReferences + LocalBranch[] + RemoteBranch + Action 回调
        // 替代 RepositoryUserControl；额外注入 workingDirectoryIsDirty + SubmodulesToUpdate（默认 empty）
        public TrackRemoteBranchWindow(
            GitModule gitModule,
            RepositoryReferences? references,
            LocalBranch[] localBranches,
            RemoteBranch remoteBranch,
            bool workingDirectoryIsDirty = false,
            SubmodulesToUpdate? submodulesToUpdate = null,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _references = references;
            _localBranches = localBranches ?? Array.Empty<LocalBranch>();
            _remoteBranch = remoteBranch ?? throw new ArgumentNullException(nameof(remoteBranch));
            _workingDirectoryIsDirty = workingDirectoryIsDirty;
            _submodulesToUpdate = submodulesToUpdate ?? new SubmodulesToUpdate(new Tuple<Submodule, bool>[0]);
            _onCompleted = onCompleted;

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Track Remote Branch");
            DialogDescription = Translate("Create new local branch which tracks remote branch");
            SubmitButtonTitle = Translate("Track");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Track Remote Branch");

            // 对照 WPF: GitPointView.Value = remoteBranch;
            RemoteBranchTextBlock.Text = _remoteBranch.Name ?? "(remote branch)";
            // 对照 WPF: LocalBranchNameTextBox.Text = _remoteBranch.ShortName;
            LocalBranchNameTextBox.Text = _remoteBranch.ShortName ?? "";

            // 对照 WPF: bool checkout_StashAndReapply = ForkPlusSettings.Default.Checkout_StashAndReapply;
            bool checkout_StashAndReapply = ForkPlusSettings.Default.Checkout_StashAndReapply;
            StashAndReapplyRadioButton.IsChecked = checkout_StashAndReapply;
            DoNotChangeRadioButton.IsChecked = !checkout_StashAndReapply;

            // 对照 WPF: if (_repositoryUserControl.RepositoryStatus.WorkingDirectoryIsDirty()) { Show } else { Collapse }
            if (_workingDirectoryIsDirty)
            {
                LocalChangesTextBlock.IsVisible = true;
                LocalChangesOptionsContainer.IsVisible = true;
            }
            else
            {
                LocalChangesTextBlock.IsVisible = false;
                LocalChangesOptionsContainer.IsVisible = false;
            }

            // 对照 WPF: spike 版省略 KeyDown/KeyUp 监听 Shift 切换 "Leave as stash" UX

            RefreshCommandPreview();
            UpdateSubmitButton();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                SetStatus(ForkPlusDialogStatus.None, string.Empty);
                string? text = LocalBranchNameTextBox.Text;
                if (string.IsNullOrEmpty(text))
                {
                    return false;
                }
                string branchName = text.ToLower();
                string? text2 = ReferenceNameValidator.Validate(branchName);
                if (text2 != null)
                {
                    SetStatus(ForkPlusDialogStatus.Warning, text2);
                    return false;
                }
                // 对照 WPF: _localBranches.AnyItem(x => x.Name.ToLower() == branchName)
                if (_localBranches.Any((LocalBranch x) => x.Name.ToLower() == branchName))
                {
                    SetStatus(ForkPlusDialogStatus.Warning, FormatTranslate("Branch '{0}' already exists", text));
                    return false;
                }
                return true;
            }
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            string? localName = LocalBranchNameTextBox.Text;
            if (string.IsNullOrWhiteSpace(localName))
            {
                return null;
            }
            RemoteBranch? remoteBranch = _remoteBranch;
            if (remoteBranch == null)
            {
                return null;
            }
            var parts = new System.Collections.Generic.List<string> { "git", "checkout" };
            if (DiscardRadioButton.IsChecked.GetValueOrDefault())
            {
                parts.Add("--force");
            }
            parts.Add("-b");
            parts.Add(localName);
            parts.Add(remoteBranch.Name);
            string command = string.Join(" ", parts);
            // 对照 WPF: if (_repositoryUserControl != null && _repositoryUserControl.RepositoryStatus.WorkingDirectoryIsDirty()
            //           && StashAndReapplyRadioButton.IsChecked.GetValueOrDefault()) command = "git stash\n" + command;
            if (_workingDirectoryIsDirty && StashAndReapplyRadioButton.IsChecked.GetValueOrDefault())
            {
                command = "git stash\n" + command;
            }
            return command;
        }

        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBox == null) return;
            string? preview = GetCommandPreview();
            CommandPreviewTextBox.Text = preview ?? string.Empty;
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            if (_references == null)
            {
                return;
            }
            string localBranchName = LocalBranchNameTextBox.Text ?? string.Empty;
            RemoteBranch remoteBranch = _remoteBranch;
            SubmodulesToUpdate submodulesToUpdate = _submodulesToUpdate;
            // 对照 WPF: string sourceString = repositoryReferences.ActiveBranch?.Name ?? repositoryReferences.HeadSha?.ToAbbreviatedString() ?? "";
            string sourceString = _references.ActiveBranch?.Name ?? _references.HeadSha?.ToAbbreviatedString() ?? "";

            StashAndReapply checkoutStashAndReapply;
            bool checkoutDiscard;
            bool leaveAsStash;
            if (StashAndReapplyRadioButton.IsChecked.GetValueOrDefault())
            {
                // 对照 WPF: if (KeyboardHelper.IsShiftDown) { Forbidden + leaveAsStash=true } else { Possible }
                // spike 版省略 shift 逻辑：直接走 Possible 路径
                checkoutStashAndReapply = StashAndReapply.Possible;
                checkoutDiscard = false;
                leaveAsStash = false;
            }
            else if (DoNotChangeRadioButton.IsChecked.GetValueOrDefault())
            {
                checkoutStashAndReapply = StashAndReapply.Forbidden;
                checkoutDiscard = false;
                leaveAsStash = false;
            }
            else
            {
                if (!DiscardRadioButton.IsChecked.GetValueOrDefault())
                {
                    return;
                }
                checkoutStashAndReapply = StashAndReapply.Forbidden;
                checkoutDiscard = true;
                leaveAsStash = false;
            }

            // 对照 WPF: ForkPlusSettings.Default.Checkout_StashAndReapply = checkoutStashAndReapply == StashAndReapply.Possible;
            ForkPlusSettings.Default.Checkout_StashAndReapply = checkoutStashAndReapply == StashAndReapply.Possible;
            ForkPlusSettings.Default.Save();

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, FormatTranslate("Tracking '{0}'...", remoteBranch.Name));

            GitModule gitModule = _gitModule;
            bool discard = checkoutDiscard;
            StashAndReapply stashAndReapply = checkoutStashAndReapply;
            bool stash = leaveAsStash;

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(FormatTranslate("Track '{0}'", remoteBranch.Name), ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                if (stash)
                {
                    // 对照 WPF: leaveAsStash 分支：SaveStashGitCommand（不 reapply）
                    GitCommandResult<bool> stashResult = new SaveStashGitCommand().Execute(
                        gitModule,
                        $"Autostash. Switch from '{sourceString}' to '{localBranchName}' {DateTime.Now}",
                        false,
                        monitor);
                    if (!stashResult.Succeeded)
                    {
                        GitCommandResult failResult = GitCommandResult.Failure(stashResult.Error);
                        Dispatcher.UIThread.Post(delegate
                        {
                            try { _onCompleted?.Invoke(failResult); } catch (Exception ex) { Log.Error("TrackRemoteBranchWindow onCompleted failed", ex); }
                            Close(failResult);
                        });
                        return;
                    }
                }

                GitCommandResult result = PerformTrackBranch(
                    gitModule, remoteBranch, localBranchName, stashAndReapply, discard, sourceString, submodulesToUpdate, monitor);

                // 对照 WPF: if (result.Error is GitCommandError.Cancelled) Close(Success) else Close(result)
                GitCommandResult closeResult = (result.Error is GitCommandError.Cancelled)
                    ? GitCommandResult.Success()
                    : result;
                Dispatcher.UIThread.Post(delegate
                {
                    try { _onCompleted?.Invoke(closeResult); } catch (Exception ex) { Log.Error("TrackRemoteBranchWindow onCompleted failed", ex); }
                    Close(closeResult);
                });
            });
        }

        // 对照 WPF: private GitCommandResult PerformTrackBranch(...)
        private GitCommandResult PerformTrackBranch(
            GitModule gitModule,
            RemoteBranch remoteBranch,
            string localBranchName,
            StashAndReapply stashAndReapply,
            bool discardLocalChanges,
            string sourceString,
            SubmodulesToUpdate submodulesToUpdate,
            JobMonitor monitor)
        {
            monitor.SetState(JobMonitorState.InProgress);
            if (stashAndReapply == StashAndReapply.Required)
            {
                Dispatcher.UIThread.Post(delegate
                {
                    SetStatus(ForkPlusDialogStatus.InProgress, Translate("Stashing..."));
                });
                if (monitor.IsCanceled)
                {
                    return GitCommandResult.Failure(new GitCommandError.Cancelled());
                }
                GitCommandResult<bool> stashResult = new SaveStashGitCommand().Execute(
                    gitModule,
                    $"Autostash. Switch from '{sourceString}' to '{localBranchName}' {DateTime.Now}",
                    false,
                    monitor);
                if (!stashResult.Succeeded)
                {
                    return GitCommandResult.Failure(stashResult.Error);
                }
            }
            if (monitor.IsCanceled)
            {
                return GitCommandResult.Failure(new GitCommandError.Cancelled());
            }
            Dispatcher.UIThread.Post(delegate
            {
                SetStatus(ForkPlusDialogStatus.InProgress, FormatTranslate("Tracking '{0}'...", remoteBranch.Name));
            });

            GitCommandResult trackResult = new CreateLocalAndTrackRemoteBranchGitCommand().Execute(
                gitModule, remoteBranch, localBranchName, monitor, discardLocalChanges);
            if (monitor.IsCanceled)
            {
                return GitCommandResult.Failure(new GitCommandError.Cancelled());
            }
            if (!trackResult.Succeeded)
            {
                // 对照 WPF: if (result.Error is CheckoutLocalChangesWouldBeOverwritten && stashAndReapply == Possible)
                //   重试 Required 路径
                if (trackResult.Error is GitCommandError.CheckoutLocalChangesWouldBeOverwritten
                    && stashAndReapply == StashAndReapply.Possible)
                {
                    monitor.AppendOutputLine("fork: failed to checkout without overwriting local changes. Trying again with stash and reapply...\n");
                    return PerformTrackBranch(gitModule, remoteBranch, localBranchName, StashAndReapply.Required, discardLocalChanges, sourceString, submodulesToUpdate, monitor);
                }
                UpdateSubmodulesIfNeeded(gitModule, submodulesToUpdate, monitor);
                return trackResult;
            }
            if (stashAndReapply == StashAndReapply.Required)
            {
                if (monitor.IsCanceled)
                {
                    return GitCommandResult.Failure(new GitCommandError.Cancelled());
                }
                GitCommandResult reapplyResult = new ApplyStashGitCommand().Execute(gitModule, "stash@{0}", true, monitor);
                if (!reapplyResult.Succeeded)
                {
                    UpdateSubmodulesIfNeeded(gitModule, submodulesToUpdate, monitor);
                    return reapplyResult;
                }
            }
            GitCommandResult submodulesResult = UpdateSubmodulesIfNeeded(gitModule, submodulesToUpdate, monitor);
            if (!submodulesResult.Succeeded)
            {
                return submodulesResult;
            }
            return trackResult;
        }

        // 对照 WPF: private GitCommandResult UpdateSubmodulesIfNeeded(...)
        private GitCommandResult UpdateSubmodulesIfNeeded(GitModule gitModule, SubmodulesToUpdate submodulesToUpdate, JobMonitor monitor)
        {
            if (submodulesToUpdate.Length == 0)
            {
                return GitCommandResult.Success();
            }
            if (monitor.IsCanceled)
            {
                return GitCommandResult.Failure(new GitCommandError.Cancelled());
            }
            Dispatcher.UIThread.Post(delegate
            {
                SetStatus(ForkPlusDialogStatus.InProgress, Translate("Updating submodules..."));
            });
            return new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
        }

        // 对照 WPF: LocalBranchName_TextChanged
        public void LocalBranchName_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: LocalChangesOption_Changed（WPF 用 RadioButton Checked 事件，Avalonia 用 IsCheckedChanged）
        public void LocalChangesOption_Changed(object? sender, RoutedEventArgs e)
        {
            RefreshCommandPreview();
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            LocalBranchNameTextBox.IsEnabled = false;
            DoNotChangeRadioButton.IsEnabled = false;
            StashAndReapplyRadioButton.IsEnabled = false;
            DiscardRadioButton.IsEnabled = false;
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

        // 对照 WPF: PreferencesLocalization.FormatCurrent(text, args)
        private static string FormatTranslate(string text, params object[] args)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.FormatCurrent(text, args);
            }
            return string.Format(text, args);
        }
    }
}
