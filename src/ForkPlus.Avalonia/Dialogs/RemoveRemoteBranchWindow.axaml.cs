using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.x：Avalonia 版 RemoveRemoteBranchWindow（真实迁移版，对照 WPF RemoveRemoteBranchWindow.xaml.cs 129 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/RemoveRemoteBranchWindow.xaml.cs：
    //   - public partial class RemoveRemoteBranchWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / RepositoryReferences _references
    //           / RemoteBranch[] _remoteBranches / LocalBranch[] _localBranches
    //   - 构造函数 (RepositoryUserControl, RemoteBranch[], RepositoryReferences)
    //     * 单个: GitPointView.Show + GitPointsContainer.Collapse, "Delete Branch" / "Delete branch from remote repository" / "Delete"
    //     * 多个: GitPointsContainer.Show + GitPointView.Collapse, "Delete Branches" / "Delete branches from remote repository" / "Delete {N} branches"
    //   - GetCommandPreview override: 多行 "git push <remote> --delete refs/heads/<branch>"
    //   - OnSubmit: AddUndoable → 循环 RemoveRemoteBranchGitCommand + UpdateTrackingReferenceGitCommand(localBranch, null)
    //     → 清理 Settings.PinnedReferences / FilterReferences → Close(Success)
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + RepositoryReferences + Action<GitCommandResult>? onCompleted 回调
    //   3. GitPointView → TextBlock 显示 branch.Name（spike 简化）
    //   4. GitPoints → ItemsControl + DataTemplate 显示 Name 列表
    //   5. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   6. spike 基类不提供 DisableEditableControls → 空实现
    //   7. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   8. Collapse/Show 扩展方法 → IsVisible = false / true
    //   9. IReadOnlyListExtensions.FirstItem → LINQ FirstOrDefault
    public partial class RemoveRemoteBranchWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly RepositoryReferences _references;
        private readonly RemoteBranch[] _remoteBranches;
        private readonly LocalBranch[] _localBranches;
        private readonly Action<GitCommandResult>? _onCompleted;

        // 构造函数签名与 WPF 不同：用 GitModule + RepositoryReferences + Action 回调替代 RepositoryUserControl
        public RemoveRemoteBranchWindow(
            GitModule gitModule,
            RemoteBranch[] remoteBranches,
            RepositoryReferences references,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _references = references ?? throw new ArgumentNullException(nameof(references));
            _remoteBranches = remoteBranches ?? throw new ArgumentNullException(nameof(remoteBranches));
            _localBranches = references.LocalBranches ?? Array.Empty<LocalBranch>();
            _onCompleted = onCompleted;

            // 对照 WPF: 单个/多个分支分支
            if (_remoteBranches.Length == 1)
            {
                // 对照 WPF: GitPointsContainer.Collapse(); GitPointView.Show(); GitPointView.Value = remoteBranch;
                BranchesContainer.IsVisible = false;
                SingleBranchTextBlock.IsVisible = true;
                SingleBranchTextBlock.Text = _remoteBranches[0].Name ?? "(remote branch)";

                DialogTitle = Translate("Delete Branch");
                DialogDescription = Translate("Delete branch from remote repository");
                StartPointTextBlock.Text = Translate("Branch:");
                SubmitButtonTitle = Translate("Delete");
                Title = Translate("Delete Branch");
            }
            else
            {
                // 对照 WPF: GitPointView.Collapse(); GitPointsContainer.Show(); GitPoints.ItemsSource = _remoteBranches;
                SingleBranchTextBlock.IsVisible = false;
                BranchesContainer.IsVisible = true;
                BranchesItemsControl.ItemsSource = _remoteBranches;

                DialogTitle = Translate("Delete Branches");
                DialogDescription = Translate("Delete branches from remote repository");
                StartPointTextBlock.Text = Translate("Branches:");
                SubmitButtonTitle = FormatTranslate("Delete {0} branches", _remoteBranches.Length);
                Title = Translate("Delete Branches");
            }
            CancelButtonTitle = Translate("Cancel");

            // 对照 WPF: InitializeComponent 期间 AddCommandPreview 已执行，但 _remoteBranches 尚未赋值，
            // 导致首次 RefreshCommandPreview 返回 null 折叠了预览。此处补刷一次以显示默认命令。
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            if (_remoteBranches == null || _remoteBranches.Length == 0)
            {
                return null;
            }
            // 与 RemoveRemoteBranchGitCommand 实际执行的 git push <remote> --delete refs/heads/<branch> 一致。
            var lines = new System.Collections.Generic.List<string>(_remoteBranches.Length);
            foreach (RemoteBranch b in _remoteBranches)
            {
                lines.Add("git push " + b.Remote + " --delete refs/heads/" + b.ShortName);
            }
            return string.Join("\n", lines);
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
            DisableEditableControls();
            // 对照 WPF: 状态栏标题国际化
            string name = ((_remoteBranches.Length > 1)
                ? FormatTranslate("Delete {0} branches", _remoteBranches.Length)
                : FormatTranslate("Delete '{0}'", _remoteBranches[0].Name));
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Deleting..."));

            // 对照 WPF: _repositoryUserControl.AddUndoable(name, delegate(JobMonitor monitor) { ... }, JobFlags.SaveToLog)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            GitModule gitModule = _gitModule;
            RemoteBranch[] remoteBranches = _remoteBranches;
            LocalBranch[] localBranches = _localBranches;
            RepositoryReferences references = _references;

            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult finalResult = GitCommandResult.Success();
                for (int i = 0; i < remoteBranches.Length; i++)
                {
                    RemoteBranch remoteBranch = remoteBranches[i];
                    Dispatcher.UIThread.Post(delegate
                    {
                        SetStatus(ForkPlusDialogStatus.InProgress, "Deleting '" + remoteBranch.Name + "'...");
                    });

                    GitCommandResult removeRemoteBranchResult = new RemoveRemoteBranchGitCommand().Execute(gitModule, remoteBranch, monitor);
                    if (!removeRemoteBranchResult.Succeeded)
                    {
                        finalResult = removeRemoteBranchResult;
                        Dispatcher.UIThread.Post(delegate
                        {
                            try { _onCompleted?.Invoke(finalResult); } catch (Exception ex) { Log.Error("RemoveRemoteBranchWindow onCompleted failed", ex); }
                            Close(finalResult);
                        });
                        return;
                    }

                    // 对照 WPF: LocalBranch localBranch = IReadOnlyListExtensions.FirstItem(_localBranches, x => x.UpstreamFullReference == remoteBranch.FullReference);
                    LocalBranch? localBranch = localBranches.FirstOrDefault((LocalBranch x) => x.UpstreamFullReference == remoteBranch.FullReference);
                    if (localBranch != null)
                    {
                        GitCommandResult removeTrackingReferenceResult = new UpdateTrackingReferenceGitCommand().Execute(gitModule, localBranch, null, monitor);
                        if (!removeTrackingReferenceResult.Succeeded)
                        {
                            finalResult = removeTrackingReferenceResult;
                            Dispatcher.UIThread.Post(delegate
                            {
                                try { _onCompleted?.Invoke(finalResult); } catch (Exception ex) { Log.Error("RemoveRemoteBranchWindow onCompleted failed", ex); }
                                Close(finalResult);
                            });
                            return;
                        }
                    }
                }

                // 对照 WPF: 清理 PinnedReferences / FilterReferences（剔除已删除分支）
                gitModule.Settings.PinnedReferences = references.PinnedReferences
                    .Where((string p) => !remoteBranches.Any((RemoteBranch b) => b.FullReference == p))
                    .ToArray();
                gitModule.Settings.FilterReferences = references.FilterReferences
                    .Where((string p) => !remoteBranches.Any((RemoteBranch b) => b.FullReference == p))
                    .ToArray();
                gitModule.Settings.Save();

                Dispatcher.UIThread.Post(delegate
                {
                    try { _onCompleted?.Invoke(GitCommandResult.Success()); } catch (Exception ex) { Log.Error("RemoveRemoteBranchWindow onCompleted failed", ex); }
                    Close(GitCommandResult.Success());
                });
            });
        }

        // spike 版：手动禁用可编辑控件（本对话框无可编辑控件，仅占位）
        private void DisableEditableControls()
        {
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
