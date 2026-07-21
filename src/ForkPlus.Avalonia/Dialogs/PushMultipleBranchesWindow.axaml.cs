using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using ForkPlus;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.x：Avalonia 版 PushMultipleBranchesWindow（spike 真实迁移版，对照 WPF PushMultipleBranchesWindow.xaml.cs 125 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/PushMultipleBranchesWindow.xaml.cs：
    //   - public partial class PushMultipleBranchesWindow : ForkPlusDialogWindow
    //   - 嵌套类 PushBranchItem (BranchName / UpstreamName / Remote / RemoteIcon ImageSource)
    //   - 字段: RepositoryUserControl _repositoryUserControl / LocalBranch[] _localBranches / Remote _remote
    //   - 构造函数 (RepositoryUserControl, LocalBranch[], Remote)
    //   - GetCommandPreview: "git push remote branch1 branch2 ..."
    //   - OnSubmit: PushMultipleBranchesGitCommand().Execute(...) → InvalidateAndRefresh(Revisions | References)
    //   - Refresh: 查找每个 localBranch 的 upstream，构建 PushBranchItem 列表
    //
    // Avalonia 版差异（spike）：
    //   1. 构造函数注入 GitModule + LocalBranch[] + Remote + RepositoryReferences + Action 回调替代 RepositoryUserControl
    //   2. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBlock
    //   3. spike 基类不提供 DisableEditableControls → 空实现（无可编辑控件）
    //   4. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   5. PreferencesLocalization → ServiceLocator.Localization.Translate
    //   6. IReadOnlyListExtensions.FirstItem → list.FirstOrDefault(predicate) (LINQ)
    //   7. PushBranchItem.RemoteIcon (ImageSource) → spike 版用 TextBlock 显示 Remote.Name 简化
    public partial class PushMultipleBranchesWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // 对照 WPF: PushBranchItem 嵌套类（spike 简化：去掉 ImageSource RemoteIcon）
        public class PushBranchItem
        {
            public string BranchName { get; }

            public string UpstreamName { get; }

            public Remote Remote { get; }

            public PushBranchItem(LocalBranch localBranch, RemoteBranch remoteBranch, Remote remote)
            {
                BranchName = localBranch.Name;
                UpstreamName = (remoteBranch != null)
                    ? remoteBranch.Name
                    : string.Format(PushMultipleBranchesWindow.Translate("{0} (new)"), remote.Name + "/" + localBranch.Name);
                Remote = remote;
            }
        }

        private readonly GitModule _gitModule;
        private readonly LocalBranch[] _localBranches;
        private readonly Remote _remote;
        private readonly RepositoryReferences _references;
        private readonly Action<GitCommandResult> _onCompleted;

        // 构造函数签名与 WPF 不同：用 GitModule + LocalBranch[] + Remote + RepositoryReferences + Action 回调替代 RepositoryUserControl
        public PushMultipleBranchesWindow(
            GitModule gitModule,
            LocalBranch[] localBranches,
            Remote remote,
            RepositoryReferences references = null,
            Action<GitCommandResult> onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _localBranches = localBranches ?? Array.Empty<LocalBranch>();
            _remote = remote ?? throw new ArgumentNullException(nameof(remote));
            _references = references ?? RepositoryReferences.Empty;
            _onCompleted = onCompleted;

            DialogTitle = Translate("Push");
            DialogDescription = string.Format(Translate("Push {0} branches to remote repository"), _localBranches.Length);
            SubmitButtonTitle = string.Format(Translate("Push {0} branches"), _localBranches.Length);
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Push");

            Refresh();
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string GetCommandPreview()
        {
            var parts = new List<string> { "git", "push", _remote.Name };
            foreach (LocalBranch localBranch in _localBranches)
            {
                parts.Add(localBranch.Name);
            }
            return string.Join(" ", parts);
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
            if (gitModule == null)
            {
                return;
            }
            Remote remote = _remote;
            LocalBranch[] localBranches = _localBranches;

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Pushing..."));

            // 对照 WPF: repositoryUserControl.JobQueue.Add(...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult pushResult = new PushMultipleBranchesGitCommand().Execute(
                    gitModule, remote.Name, localBranches, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    try
                    {
                        _onCompleted?.Invoke(pushResult);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("PushMultipleBranchesWindow onCompleted callback failed", ex);
                    }
                    Close(pushResult);
                });
            });
        }

        private void Refresh()
        {
            RemoteBranch[] allRemoteBranches = _references?.RemoteBranches;
            if (allRemoteBranches != null)
            {
                List<RemoteBranch> remoteBranches = allRemoteBranches
                    .Where(x => x.Remote == _remote.Name)
                    .ToList();
                var list = new List<PushBranchItem>(4);
                foreach (LocalBranch localBranch in _localBranches)
                {
                    RemoteBranch remoteBranch = FindUpstream(localBranch, remoteBranches);
                    list.Add(new PushBranchItem(localBranch, remoteBranch, _remote));
                }
                BranchesItemsControl.ItemsSource = list.ToArray();
            }
        }

        private static RemoteBranch FindUpstream(LocalBranch localBranch, IReadOnlyList<RemoteBranch> remoteBranches)
        {
            string upstreamFullReference = localBranch?.UpstreamFullReference;
            if (upstreamFullReference == null)
            {
                return null;
            }
            return remoteBranches.FirstOrDefault(x => x.FullReference == upstreamFullReference);
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
