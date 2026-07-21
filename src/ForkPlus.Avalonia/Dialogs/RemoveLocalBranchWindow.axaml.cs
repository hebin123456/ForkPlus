using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.x：Avalonia 版 RemoveLocalBranchWindow（真实迁移版，对照 WPF RemoveLocalBranchWindow.xaml.cs 373 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/RemoveLocalBranchWindow.xaml.cs：
    //   - public partial class RemoveLocalBranchWindow : ForkPlusDialogWindow
    //   - 嵌套类 RemoveLocalBranchItem : INotifyPropertyChanged
    //     (BranchName / UpstreamName / RemoteName / RemoteIcon / UpstreamVisibility)
    //   - 字段: RepositoryUserControl _repositoryUserControl / LocalBranch[] _branchesToRemove
    //     / RemoteBranch[] _remoteBranches / RepositoryRemotes _remotes / RepositoryReferences _references
    //     / RemoveLocalBranchItem[] _branchesSource / Worktree? _worktreeToRemove
    //   - 单分支 vs 多分支两种 UI 模式：
    //     * 单分支：GitPointView 显示分支名 + 可选 DeleteRemoteBranchCheckBox + 可选 DeleteWorktreeCheckBox
    //     * 多分支：ItemsControl 显示分支列表 + DeleteRemoteBranchCheckBox
    //   - GetCommandPreview: "git branch -D {names}" + 可选 "git push {remote} --delete {shortName}" 多行
    //   - OnSubmit: AddUndoable → RemoveWorktreeGitCommand (可选) + RemoveLocalBranchGitCommand
    //     + 可选 RemoveMultipleRemoteBranchesGitCommand → 更新 Pinned/Filter references → Close(result)
    //
    // Avalonia 版差异（spike）：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + RepositoryReferences + RepositoryRemotes
    //      + Action<GitCommandResult>? onCompleted + Action<string>? onCloseTab 回调
    //   3. GitPointView 自定义控件 → spike 版用 TextBlock 显示 branch.Name 简化
    //   4. ItemsControl + DataTemplate 显示多分支列表（spike 版简化 RemoveLocalBranchItem：
    //      不用 INotifyPropertyChanged，UpstreamVisibility 切换改为始终显示 upstream 信息）
    //   5. PNG 图标（WarningImage / WorktreeWarningImage / RemoteIcon）→ spike 版用 emoji TextBlock "⚠" 替代
    //   6. MainWindow.Instance.TabManager.CloseTab(path) → 注入 Action<string>? onCloseTab 回调
    //   7. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   8. spike 基类不提供 DisableEditableControls → 手动禁用 CheckBoxes
    //   9. JobQueue + AddUndoable + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor（spike 版不做 undo）
    //  10. CheckBox Checked/Unchecked 事件 → IsCheckedChanged 事件
    //  11. Collapse()/Show() 扩展方法 → IsVisible = false/true
    //  12. PreferencesLocalization → ServiceLocator.Localization.Translate/FormatCurrent
    public partial class RemoveLocalBranchWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // spike 版：简化 RemoveLocalBranchItem（不用 INotifyPropertyChanged，始终显示 upstream 信息）
        // 对照 WPF: public class RemoveLocalBranchItem : INotifyPropertyChanged
        public class RemoveLocalBranchItem
        {
            public string BranchName { get; }

            public string UpstreamName { get; }

            public string RemoteName { get; }

            // spike 版：合并显示字符串（DataTemplate 绑定此属性）
            public string DisplayText => string.IsNullOrEmpty(UpstreamName)
                ? BranchName
                : BranchName + " → " + UpstreamName;

            public RemoveLocalBranchItem(LocalBranch localBranch, RemoteBranch remoteBranch, Remote remote)
            {
                BranchName = localBranch?.Name ?? "";
                UpstreamName = remoteBranch?.Name;
                RemoteName = remote?.Name;
            }
        }

        private readonly GitModule _gitModule;
        private readonly LocalBranch[] _branchesToRemove;
        private readonly RemoteBranch[] _remoteBranches;
        private readonly RepositoryRemotes _remotes;
        private readonly RepositoryReferences _references;
        private readonly Worktree? _worktreeToRemove;
        private readonly Action<GitCommandResult>? _onCompleted;
        private readonly Action<string>? _onCloseTab;

        // 构造函数签名与 WPF 不同：注入 GitModule + Action 回调替代 RepositoryUserControl 依赖
        public RemoveLocalBranchWindow(
            GitModule gitModule,
            RepositoryReferences references,
            LocalBranch[] branchesToRemove,
            RepositoryRemotes remotes,
            Worktree? worktreeToRemove = null,
            Action<GitCommandResult>? onCompleted = null,
            Action<string>? onCloseTab = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _branchesToRemove = branchesToRemove ?? throw new ArgumentNullException(nameof(branchesToRemove));
            _references = references ?? throw new ArgumentNullException(nameof(references));
            _remotes = remotes ?? RepositoryRemotes.Empty;
            _remoteBranches = references.RemoteBranches ?? Array.Empty<RemoteBranch>();
            _worktreeToRemove = worktreeToRemove;
            _onCompleted = onCompleted;
            _onCloseTab = onCloseTab;

            if (_branchesToRemove.Length == 1)
            {
                // 对照 WPF: 单分支模式
                DialogTitle = Translate("Delete Branch");
                DialogDescription = Translate("Delete local branch from your repository");
                StartPointTextBlock.Text = Translate("Branch:");
                SubmitButtonTitle = Translate("Delete");
                CancelButtonTitle = Translate("Cancel");
                Title = Translate("Delete Branch");

                LocalBranch localBranch = _branchesToRemove[0];
                // 对照 WPF: BranchesContainer.Collapse(); GitPointView.Show(); GitPointView.Value = localBranch;
                BranchesContainer.IsVisible = false;
                SingleBranchTextBlock.IsVisible = true;
                SingleBranchTextBlock.Text = localBranch.Name;

                RemoteBranch remoteBranch = FindUpstream(localBranch, _remoteBranches);
                if (remoteBranch != null)
                {
                    DeleteRemoteBranchCheckBox.Content = Translate("Also delete remote branch");
                    DeleteRemoteBranchCheckBox.IsEnabled = true;
                    DeleteRemoteBranchCheckBoxUpstream.IsVisible = true;
                    DeleteRemoteBranchCheckBoxUpstream.Text = remoteBranch.Name ?? "";
                }
                else
                {
                    DeleteRemoteBranchCheckBox.Content = Translate("Also delete corresponding remote branch");
                    DeleteRemoteBranchCheckBox.IsEnabled = false;
                    DeleteRemoteBranchCheckBoxUpstream.IsVisible = false;
                }

                if (_worktreeToRemove.HasValue)
                {
                    DeleteWorktreeContainer.IsVisible = true;
                    DeleteWorktreeLabel.Text = _worktreeToRemove.Value.FriendlyName;
                }
                else
                {
                    DeleteWorktreeContainer.IsVisible = false;
                }
            }
            else
            {
                // 对照 WPF: 多分支模式
                DialogTitle = Translate("Delete Branches");
                DialogDescription = Translate("Delete local branches from your repository");
                StartPointTextBlock.Text = Translate("Branches:");
                DeleteRemoteBranchCheckBox.Content = Translate("Also delete corresponding remote branches");
                SubmitButtonTitle = FormatTranslate("Delete {0} branches", _branchesToRemove.Length);
                CancelButtonTitle = Translate("Cancel");
                Title = Translate("Delete Branches");

                SingleBranchTextBlock.IsVisible = false;
                BranchesContainer.IsVisible = true;
                DeleteRemoteBranchCheckBox.IsEnabled = AtLeastOneBranchHasUpstream(_branchesToRemove, _remoteBranches);

                List<RemoveLocalBranchItem> list = new List<RemoveLocalBranchItem>(_branchesToRemove.Length);
                foreach (LocalBranch localBranch in _branchesToRemove)
                {
                    RemoteBranch remoteBranch = FindUpstream(localBranch, _remoteBranches);
                    Remote remote = GetRemote(remoteBranch, _remotes);
                    list.Add(new RemoveLocalBranchItem(localBranch, remoteBranch, remote));
                }
                BranchesItemsControl.ItemsSource = list;

                if (_worktreeToRemove.HasValue)
                {
                    DeleteWorktreeContainer.IsVisible = true;
                    DeleteWorktreeLabel.Text = _worktreeToRemove.Value.FriendlyName;
                }
                else
                {
                    DeleteWorktreeContainer.IsVisible = false;
                }
            }

            // 对照 WPF: RefreshCommandPreview()（InitializeComponent 后 _branchesToRemove 已赋值，补刷一次显示默认命令）
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string GetCommandPreview()
        {
            if (_branchesToRemove == null || _branchesToRemove.Length == 0)
            {
                return null;
            }
            // 与 RemoveLocalBranchGitCommand 实际执行的 --delete --force 一致
            var parts = new List<string> { "git", "branch", "-D" };
            foreach (LocalBranch b in _branchesToRemove)
            {
                parts.Add(b.Name);
            }
            string command = string.Join(" ", parts);
            if (DeleteRemoteBranchCheckBox.IsChecked.GetValueOrDefault())
            {
                foreach (LocalBranch b in _branchesToRemove)
                {
                    RemoteBranch upstream = FindUpstream(b, _remoteBranches);
                    Remote remote = GetRemote(upstream, _remotes);
                    if (upstream != null && remote != null)
                    {
                        command += "\ngit push " + remote.Name + " --delete " + upstream.ShortName;
                    }
                }
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
            LocalBranch[] branchesToRemove = _branchesToRemove;
            RemoteBranch[] remoteBranches = _remoteBranches;
            RepositoryRemotes remotes = _remotes;
            bool removeUpstreams = DeleteRemoteBranchCheckBox.IsChecked.GetValueOrDefault();
            List<string> pinned = new List<string>(_references.PinnedReferences);
            List<string> filtered = new List<string>(_references.FilterReferences);
            bool removeWorktree = DeleteWorktreeCheckBox.IsChecked.GetValueOrDefault();
            Worktree? worktreeToRemove = _worktreeToRemove;

            string name = (branchesToRemove.Length > 1)
                ? string.Format(Translate("Delete {0} branches"), branchesToRemove.Length)
                : string.Format(Translate("Delete '{0}'"), branchesToRemove[0].Name);

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Deleting..."));

            // 对照 WPF: _repositoryUserControl.AddUndoable(name, ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor（spike 版不做 undo）
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult finalResult = GitCommandResult.Success();

                // 对照 WPF: 先删 worktree（可选）
                if (removeWorktree && worktreeToRemove.HasValue)
                {
                    Worktree worktree = worktreeToRemove.Value;
                    Dispatcher.UIThread.Post(delegate
                    {
                        SetStatus(ForkPlusDialogStatus.InProgress, Translate("Deleting worktree..."));
                    });
                    GitCommandResult removeWorktreeResult = new RemoveWorktreeGitCommand().Execute(gitModule, worktree.Path, monitor);
                    if (!removeWorktreeResult.Succeeded)
                    {
                        Dispatcher.UIThread.Post(delegate
                        {
                            try { _onCompleted?.Invoke(removeWorktreeResult); } catch (Exception ex) { Log.Error("RemoveLocalBranchWindow onCompleted callback failed", ex); }
                            Close(removeWorktreeResult);
                        });
                        return;
                    }
                    // 对照 WPF: MainWindow.Instance.TabManager.CloseTab(worktreeToRemove.Value.Path);
                    // Avalonia spike: 调用注入的回调
                    try { _onCloseTab?.Invoke(worktree.Path); } catch (Exception ex) { Log.Error("RemoveLocalBranchWindow onCloseTab callback failed", ex); }
                }

                // 对照 WPF: 删本地分支
                Dispatcher.UIThread.Post(delegate
                {
                    SetStatus(ForkPlusDialogStatus.InProgress, Translate("Deleting..."));
                });
                string[] branchNames = branchesToRemove.Map((LocalBranch x) => x.Name);
                GitCommandResult removeLocalBranchResult = new RemoveLocalBranchGitCommand().Execute(gitModule, branchNames, monitor);
                if (!removeLocalBranchResult.Succeeded)
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        try { _onCompleted?.Invoke(removeLocalBranchResult); } catch (Exception ex) { Log.Error("RemoveLocalBranchWindow onCompleted callback failed", ex); }
                        Close(removeLocalBranchResult);
                    });
                    return;
                }

                // 对照 WPF: 从 pinned/filtered 中移除已删分支
                foreach (LocalBranch localBranch in branchesToRemove)
                {
                    pinned.Remove(localBranch.FullReference);
                    filtered.Remove(localBranch.FullReference);
                }

                // 对照 WPF: 删远端分支（可选）
                if (removeUpstreams)
                {
                    // branchesToRemove.CompactMap(x => x.UpstreamFullReference).CompactMap(x => FirstItem(remoteBranches, y => y.FullReference == x))
                    RemoteBranch[] upstreamsToDelete = branchesToRemove
                        .CompactMap((LocalBranch x) => x.UpstreamFullReference)
                        .CompactMap((string x) => IReadOnlyListExtensions.FirstItem(remoteBranches, (RemoteBranch y) => y.FullReference == x));

                    if (upstreamsToDelete.Length != 0)
                    {
                        // 按 Remote 分组
                        Dictionary<string, List<RemoteBranch>> groupsByRemote = new Dictionary<string, List<RemoteBranch>>();
                        foreach (RemoteBranch rb in upstreamsToDelete)
                        {
                            if (!groupsByRemote.TryGetValue(rb.Remote, out var list))
                            {
                                list = new List<RemoteBranch>();
                                groupsByRemote[rb.Remote] = list;
                            }
                            list.Add(rb);
                        }

                        GitCommandResult removeRemoteBranchesResult = GitCommandResult.Success();
                        foreach (KeyValuePair<string, List<RemoteBranch>> group in groupsByRemote)
                        {
                            Remote remote = IReadOnlyListExtensions.FirstItem(remotes.Items, (Remote x) => x.Name == group.Key);
                            if (remote == null) continue;

                            RemoteBranch[] branchesInGroup = group.Value.ToArray();
                            string title = (branchesInGroup.Length > 1)
                                ? string.Format(Translate("Deleting {0} remote branches..."), branchesInGroup.Length)
                                : string.Format(Translate("Deleting '{0}'..."), branchesInGroup[0].Name);
                            Dispatcher.UIThread.Post(delegate
                            {
                                SetStatus(ForkPlusDialogStatus.InProgress, title);
                            });
                            GitCommandResult groupResult = new RemoveMultipleRemoteBranchesGitCommand().Execute(gitModule, branchesInGroup, remote, monitor);
                            if (!groupResult.Succeeded)
                            {
                                removeRemoteBranchesResult = groupResult;
                            }
                            else
                            {
                                foreach (RemoteBranch rb in branchesInGroup)
                                {
                                    pinned.Remove(rb.FullReference);
                                    filtered.Remove(rb.FullReference);
                                }
                            }
                        }

                        if (!removeRemoteBranchesResult.Succeeded)
                        {
                            Dispatcher.UIThread.Post(delegate
                            {
                                try { _onCompleted?.Invoke(removeRemoteBranchesResult); } catch (Exception ex) { Log.Error("RemoveLocalBranchWindow onCompleted callback failed", ex); }
                                Close(removeRemoteBranchesResult);
                            });
                            return;
                        }
                    }
                }

                // 对照 WPF: 保存 Pinned/Filter references
                try
                {
                    gitModule.Settings.PinnedReferences = pinned.ToArray();
                    gitModule.Settings.FilterReferences = filtered.ToArray();
                    gitModule.Settings.Save();
                }
                catch (Exception ex)
                {
                    Log.Error("RemoveLocalBranchWindow: failed to save repository settings", ex);
                }

                Dispatcher.UIThread.Post(delegate
                {
                    try { _onCompleted?.Invoke(GitCommandResult.Success()); } catch (Exception ex) { Log.Error("RemoveLocalBranchWindow onCompleted callback failed", ex); }
                    Close(GitCommandResult.Success());
                });
            });
        }

        // 对照 WPF: DeleteRemoteBranchCheckBox_Changed（WPF 用 Checked/Unchecked 两个事件，Avalonia 用 IsCheckedChanged）
        public void DeleteRemoteBranchCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            // 对照 WPF: WarningImage.Show()/Collapse()
            WarningText.IsVisible = DeleteRemoteBranchCheckBox.IsChecked.GetValueOrDefault();
            RefreshCommandPreview();
        }

        // 对照 WPF: DeleteWorktreeCheckBox_Changed
        public void DeleteWorktreeCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            // 对照 WPF: WorktreeWarningImage.Show()/Collapse()
            WorktreeWarningText.IsVisible = DeleteWorktreeCheckBox.IsChecked.GetValueOrDefault();
            RefreshCommandPreview();
        }

        // spike 版：手动禁用可编辑控件（基类不提供 DisableEditableControls）
        private void DisableEditableControls()
        {
            DeleteRemoteBranchCheckBox.IsEnabled = false;
            DeleteWorktreeCheckBox.IsEnabled = false;
        }

        // 对照 WPF: private static bool AtLeastOneBranchHasUpstream(...)
        private static bool AtLeastOneBranchHasUpstream(LocalBranch[] localBranches, RemoteBranch[] remoteBranches)
        {
            return localBranches.AnyItem((LocalBranch x) => FindUpstream(x, remoteBranches) != null);
        }

        // 对照 WPF: private static RemoteBranch FindUpstream(...)
        private static RemoteBranch FindUpstream(LocalBranch localBranch, RemoteBranch[] remoteBranches)
        {
            string upstream = localBranch.UpstreamFullReference;
            if (upstream == null)
            {
                return null;
            }
            return IReadOnlyListExtensions.FirstItem(remoteBranches, (RemoteBranch x) => x.FullReference == upstream);
        }

        // 对照 WPF: private static Remote GetRemote(...)
        private static Remote GetRemote(RemoteBranch remoteBranch, RepositoryRemotes remotes)
        {
            if (remoteBranch == null) return null;
            return IReadOnlyListExtensions.FirstItem(remotes.Items, (Remote x) => x.Name == remoteBranch.Remote);
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
