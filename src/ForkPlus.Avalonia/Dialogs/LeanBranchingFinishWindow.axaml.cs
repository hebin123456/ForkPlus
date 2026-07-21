using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Commands.LeanBranching;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.x：Avalonia 版 LeanBranchingFinishWindow（真实迁移版，对照 WPF LeanBranchingFinishWindow.xaml.cs 279 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/LeanBranchingFinishWindow.xaml.cs：
    //   - public partial class LeanBranchingFinishWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl
    //   - 构造函数 (RepositoryUserControl)
    //     * LocalBranch activeBranch = repositoryData.References.ActiveBranch
    //     * LocalBranch localBranch = repositoryData.References.LocalMain(gitModule)
    //     * DialogDescription = FormatTranslate("Finish '{0}' and merge it into '{1}'", active, local)
    //     * CurrentBranchGitPointView.Value = activeBranch / MainBranchGitPointView.Value = localBranch
    //   - IsSubmitAllowed: 多层 BehindAheadCount 校验
    //     (active 与 upstream 同步 + localMain 与 remoteMain 同步 + active 与 localMain 同步)
    //   - GetCommandPreview: 可选 "git fetch" + "git checkout main" + "git merge <active>"
    //   - OnSubmit: 可选 FastForwardGitCommand(main, remoteMain)
    //     → CheckoutBranchGitCommand(localMain) → MergeGitCommand(active, FastForward|NoFastForward)
    //     → 可选 UpdateSubmodulesGitCommand → Close(result)
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + RepositoryReferences? + CommitGraphCache?
    //      + SubmodulesToUpdate + Action<GitCommandResult>? onCompleted 回调
    //   3. GitPointView 自定义控件 → spike 版用 TextBlock 显示 branch.Name 简化
    //   4. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   5. spike 基类不提供 DisableEditableControls → 空实现（本对话框无可编辑控件）
    //   6. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   7. ErrorWindow 显示校验失败 → spike 版简化为 return（IsSubmitAllowed 已阻止提交）
    //   8. BehindAheadCount.AreInSync 扩展方法在 ForkPlus.Git.Commands.LeanBranching 命名空间
    //   9. RepositoryReferences.LocalMain / Upstream 扩展方法在 ForkPlus.Git.Commands.LeanBranching 命名空间
    public partial class LeanBranchingFinishWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly RepositoryReferences? _repositoryReferences;
        private readonly CommitGraphCache? _commitGraphCache;
        private readonly SubmodulesToUpdate _submodulesToUpdate;
        private readonly Action<GitCommandResult>? _onCompleted;

        // 构造函数签名与 WPF 不同：用 GitModule + RepositoryReferences? + CommitGraphCache?
        // + SubmodulesToUpdate + Action 回调替代 RepositoryUserControl 依赖
        public LeanBranchingFinishWindow(
            GitModule gitModule,
            RepositoryReferences? references = null,
            CommitGraphCache? commitGraphCache = null,
            SubmodulesToUpdate submodulesToUpdate = default,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _repositoryReferences = references;
            _commitGraphCache = commitGraphCache;
            _submodulesToUpdate = submodulesToUpdate;
            _onCompleted = onCompleted;

            // 对照 WPF: LocalBranch activeBranch = repositoryData.References.ActiveBranch;
            //           LocalBranch localBranch = repositoryData.References.LocalMain(gitModule);
            LocalBranch activeBranch = references?.ActiveBranch;
            LocalBranch localBranch = references?.LocalMain(gitModule);

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Finish Branch");
            DialogDescription = string.Format(
                Translate("Finish '{0}' and merge it into '{1}'"),
                activeBranch?.Name ?? "",
                localBranch?.Name ?? "");
            SubmitButtonTitle = Translate("Finish");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Finish Branch");

            // 对照 WPF: CurrentBranchGitPointView.Value = activeBranch; MainBranchGitPointView.Value = localBranch;
            // Avalonia spike: 用 TextBlock 显示名称简化
            CurrentBranchGitPointTextBlock.Text = activeBranch?.Name ?? "";
            MainBranchGitPointTextBlock.Text = localBranch?.Name ?? "";

            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                GitModule gitModule = _gitModule;
                if (gitModule == null) return false;
                RepositoryReferences? references = _repositoryReferences;
                if (references == null) return false;
                CommitGraphCache? commitGraphCache = _commitGraphCache;
                if (commitGraphCache == null) return false;

                LocalBranch localBranch = references.LocalMain(gitModule);
                if (localBranch == null) return false;
                RemoteBranch remoteBranch = references.Upstream(localBranch);
                if (remoteBranch == null) return false;

                LocalBranch activeBranch = references.ActiveBranch;
                if (activeBranch == null) return false;
                RemoteBranch remoteBranch2 = references.Upstream(activeBranch);
                if (remoteBranch2 != null)
                {
                    GitCommandResult<BehindAheadCount> activeUpstreamResult =
                        new GetBehindAheadCountGitCommand().Execute(gitModule, activeBranch.Sha, remoteBranch2.Sha, commitGraphCache);
                    if (!activeUpstreamResult.Succeeded) return false;
                    if (activeUpstreamResult.Result.Right > 0)
                    {
                        SetStatus(ForkPlusDialogStatus.Warning, string.Format(Translate("You must sync '{0}' first"), activeBranch.Name));
                        return false;
                    }
                }
                GitCommandResult<BehindAheadCount> localMainResult =
                    new GetBehindAheadCountGitCommand().Execute(gitModule, localBranch.Sha, remoteBranch.Sha, commitGraphCache);
                if (!localMainResult.Succeeded) return false;
                if (!localMainResult.Result.AreInSync())
                {
                    SetStatus(ForkPlusDialogStatus.Warning, string.Format(Translate("You must checkout and sync '{0}' first"), localBranch.Name));
                    return false;
                }
                GitCommandResult<BehindAheadCount> activeLocalMainResult =
                    new GetBehindAheadCountGitCommand().Execute(gitModule, activeBranch.Sha, localBranch.Sha, commitGraphCache);
                if (!activeLocalMainResult.Succeeded) return false;
                if (!activeLocalMainResult.Result.AreInSync())
                {
                    SetStatus(ForkPlusDialogStatus.Warning, string.Format(Translate("You must sync '{0}' with '{1}' first"), activeBranch.Name, localBranch.Name));
                    return false;
                }
                return true;
            }
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            GitModule gitModule = _gitModule;
            if (gitModule == null) return null;
            RepositoryReferences? references = _repositoryReferences;
            if (references == null) return null;

            LocalBranch localMain = references.LocalMain(gitModule);
            LocalBranch activeBranch = references.ActiveBranch;
            if (localMain == null || activeBranch == null) return null;

            var lines = new List<string>();
            RemoteBranch remoteMain = references.Upstream(localMain);
            if (remoteMain != null)
            {
                lines.Add("git fetch " + remoteMain.Remote + " " + remoteMain.ShortName);
            }
            lines.Add("git checkout " + localMain.Name);
            lines.Add("git merge " + activeBranch.Name);
            return string.Join("\n", lines);
        }

        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBox == null) return;
            CommandPreviewTextBox.Text = GetCommandPreview() ?? string.Empty;
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            GitModule gitModule = _gitModule;
            if (gitModule == null) return;
            RepositoryReferences? references = _repositoryReferences;
            if (references == null) return;
            CommitGraphCache? commitGraphCache = _commitGraphCache;
            if (commitGraphCache == null) return;

            LocalBranch localMain = references.LocalMain(gitModule);
            if (localMain == null) return;
            RemoteBranch remoteMain = references.Upstream(localMain);
            if (remoteMain == null) return;
            LocalBranch activeBranch = references.ActiveBranch;
            if (activeBranch == null) return;
            SubmodulesToUpdate submodulesToUpdate = _submodulesToUpdate;

            // 对照 WPF: 主仓库 behind/ahead 预检
            GitCommandResult<BehindAheadCount> mainBehindAheadCountResponse =
                new GetBehindAheadCountGitCommand().Execute(gitModule, localMain.Sha, remoteMain.Sha, commitGraphCache);
            if (!mainBehindAheadCountResponse.Succeeded)
            {
                // spike 版简化：WPF 显示 ErrorWindow，spike 直接 return（IsSubmitAllowed 已阻止提交）
                return;
            }
            GitCommandResult<BehindAheadCount> behindAheadCountResponse =
                new GetBehindAheadCountGitCommand().Execute(gitModule, activeBranch.Sha, localMain.Sha, commitGraphCache);
            if (!behindAheadCountResponse.Succeeded)
            {
                return;
            }

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, string.Format(Translate("Finishing {0}..."), activeBranch.Name));

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(string.Format(Translate("Finish '{0}'"), activeBranch.Name), ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                Dispatcher.UIThread.Post(delegate
                {
                    SetStatus(ForkPlusDialogStatus.InProgress,
                        string.Format(Translate("Fast-forward '{0}' to '{1}'"), localMain.Name, remoteMain.Name));
                });

                // 对照 WPF: if (mainBehindAheadCountResponse.Result.Right > 0) { FastForwardGitCommand... }
                if (mainBehindAheadCountResponse.Result.Right > 0)
                {
                    GitCommandResult mainFastForwardResult = new FastForwardGitCommand().Execute(gitModule, localMain, monitor);
                    if (!mainFastForwardResult.Succeeded)
                    {
                        Dispatcher.UIThread.Post(delegate
                        {
                            InvokeOnCompleted(mainFastForwardResult);
                            Close(mainFastForwardResult);
                        });
                        return;
                    }
                }

                Dispatcher.UIThread.Post(delegate
                {
                    SetStatus(ForkPlusDialogStatus.InProgress, Translate("Checkout..."));
                });
                GitCommandResult checkoutResult = new CheckoutBranchGitCommand().Execute(gitModule, localMain, monitor);
                if (!checkoutResult.Succeeded && !(checkoutResult.Error is GitCommandError.Cancelled))
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        InvokeOnCompleted(checkoutResult);
                        Close(checkoutResult);
                    });
                    return;
                }

                // 对照 WPF: 根据 LeanBranchingNoFastForward 和 behindAheadCountResponse.Result.Left 选择 merge type
                MergeType mergeType;
                if (!gitModule.Settings.LeanBranchingNoFastForward && behindAheadCountResponse.Result.Left == 1)
                {
                    mergeType = MergeType.FastForward;
                    monitor.AppendOutputLine("'" + activeBranch.Name + "' consists of a single commit. Using fast-forward");
                }
                else
                {
                    mergeType = MergeType.NoFastForward;
                }

                Dispatcher.UIThread.Post(delegate
                {
                    SetStatus(ForkPlusDialogStatus.InProgress,
                        string.Format(Translate("Merging into '{0}'..."), localMain.Name));
                });
                GitCommandResult mergeResult = new MergeGitCommand().Execute(
                    gitModule, activeBranch, mergeType, references, monitor);
                if (!mergeResult.Succeeded)
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        InvokeOnCompleted(mergeResult);
                        Close(mergeResult);
                    });
                    return;
                }

                if (submodulesToUpdate.Length > 0)
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        SetStatus(ForkPlusDialogStatus.InProgress, Translate("Updating submodules..."));
                    });
                    GitCommandResult updateSubmodulesResult = new UpdateSubmodulesGitCommand().Execute(
                        gitModule, submodulesToUpdate, monitor);
                    if (!updateSubmodulesResult.Succeeded)
                    {
                        Dispatcher.UIThread.Post(delegate
                        {
                            InvokeOnCompleted(updateSubmodulesResult);
                            Close(updateSubmodulesResult);
                        });
                        return;
                    }
                }

                Dispatcher.UIThread.Post(delegate
                {
                    InvokeOnCompleted(mergeResult);
                    Close(mergeResult);
                });
            });
        }

        private void InvokeOnCompleted(GitCommandResult result)
        {
            try
            {
                _onCompleted?.Invoke(result);
            }
            catch (Exception ex)
            {
                Log.Error("LeanBranchingFinishWindow onCompleted callback failed", ex);
            }
        }

        // spike 版：手动禁用可编辑控件（本对话框无可编辑控件）
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
    }
}
