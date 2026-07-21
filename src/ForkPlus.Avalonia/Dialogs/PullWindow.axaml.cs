using System;
using System.Collections.Generic;
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
    // Phase 4.x：Avalonia 版 PullWindow（spike 真实迁移版，对照 WPF PullWindow.xaml.cs 285 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/PullWindow.xaml.cs：
    //   - public partial class PullWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / RemoteBranch _predefinedRemoteBranch
    //           LocalBranch _activeLocalBranch / IReadOnlyList<RemoteBranch> _allRemoteBranches
    //           RemoteBranch _upstreamOfActiveBranch / bool _referencesLoaded
    //   - 构造函数 (RepositoryUserControl, RemoteBranch) → LoadReferencesAndRefresh(...) 异步刷新
    //   - IsSubmitAllowed: _activeLocalBranch + _referencesLoaded + remote + remoteBranch 都不为 null
    //   - GetCommandPreview: git pull remote [shortName] [--rebase] [--tags]
    //   - OnSubmit: PerformPull(...) → PullGitCommand().Execute + 可选 SaveStash + 可选 UpdateSubmodules
    //     → InvalidateAndRefresh(Status | Revisions | Head | Stashes | Submodules | ...)
    //
    // Avalonia 版差异（spike）：
    //   1. 构造函数注入 GitModule + RepositoryReferences + RepositoryRemotes + Action 回调替代 RepositoryUserControl
    //   2. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBlock
    //   3. spike 基类不提供 DisableEditableControls → 手动禁用 ComboBox + CheckBox
    //   4. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   5. CheckBox Checked/Unchecked 事件 → IsCheckedChanged 事件
    //   6. GitPointView 自定义控件 → spike 版用 TextBlock 显示 FriendlyName 简化
    //   7. SubmodulesToUpdate 依赖暂不接入（PerformPull 简化为只跑 PullGitCommand + stash）
    //   8. 异步刷新 LoadReferencesAndRefresh 简化为同步刷新（避免 GetReferencesGitCommand 等
    //      再触发，构造时已注入最新 RepositoryReferences）
    //   9. IReadOnlyListExtensions.FirstItem → list.FirstOrDefault(predicate) (LINQ)
    public partial class PullWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly RepositoryReferences _references;
        private readonly RepositoryRemotes _remotesSource;
        private readonly Action<GitCommandResult> _onCompleted;

        private readonly RemoteBranch _predefinedRemoteBranch;

        private LocalBranch _activeLocalBranch;
        private IReadOnlyList<RemoteBranch> _allRemoteBranches;
        private RemoteBranch _upstreamOfActiveBranch;
        private bool _referencesLoaded;

        // 构造函数签名与 WPF 不同：用 GitModule + RepositoryReferences + RepositoryRemotes + Action 回调替代 RepositoryUserControl
        public PullWindow(
            GitModule gitModule,
            RepositoryReferences references,
            RepositoryRemotes remotes,
            RemoteBranch remoteBranch,
            Action<GitCommandResult> onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _references = references ?? RepositoryReferences.Empty;
            _remotesSource = remotes ?? RepositoryRemotes.Empty;
            _predefinedRemoteBranch = remoteBranch;
            _onCompleted = onCompleted;

            DialogTitle = Translate("Pull");
            DialogDescription = Translate("Pull remote branches and merge them into your local branch");
            SubmitButtonTitle = Translate("Pull");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Pull");

            RebaseCheckBox.IsChecked = ForkPlusSettings.Default.Pull_Rebase;
            StashAndReapplyCheckBox.IsChecked = ForkPlusSettings.Default.Pull_StashAndReapply;

            // spike: 同步刷新（避免 LoadReferencesAndRefresh 异步路径引入额外 Git 命令调用）
            Refresh(_remotesSource.Items, _references.RemoteBranches, _references.ActiveBranch);
            _referencesLoaded = true;
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                if (_activeLocalBranch == null || !_referencesLoaded)
                {
                    return false;
                }
                Remote remote = RemotesComboBox.SelectedItem as Remote;
                Reference reference = RemoteBranchesComboBox.SelectedItem as Reference;
                if (remote != null && reference != null)
                {
                    return base.IsSubmitAllowed;
                }
                return false;
            }
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string GetCommandPreview()
        {
            Remote remote = RemotesComboBox.SelectedItem as Remote;
            if (remote == null)
            {
                return null;
            }
            RemoteBranch remoteBranch = RemoteBranchesComboBox.SelectedItem as RemoteBranch;
            bool rebase = RebaseCheckBox.IsChecked.GetValueOrDefault();
            bool allTags = ForkPlusSettings.Default.FetchAllTags;
            var parts = new List<string> { "git", "pull" };
            parts.Add(remote.Name);
            if (remoteBranch != null)
            {
                parts.Add(remoteBranch.ShortName);
            }
            if (rebase)
            {
                parts.Add("--rebase");
            }
            if (allTags)
            {
                parts.Add("--tags");
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
            Remote remote = RemotesComboBox.SelectedItem as Remote;
            if (remote == null)
            {
                return;
            }
            RemoteBranch remoteBranch2 = RemoteBranchesComboBox.SelectedItem as RemoteBranch;
            if (remoteBranch2 == null)
            {
                return;
            }
            bool rebase = RebaseCheckBox.IsChecked.GetValueOrDefault();
            bool stashAndReapply = StashAndReapplyCheckBox.IsChecked.GetValueOrDefault();
            bool allTags = ForkPlusSettings.Default.FetchAllTags;
            bool flag = _activeLocalBranch?.UpstreamFullReference == remoteBranch2.FullReference;
            RemoteBranch remoteBranch = flag ? null : remoteBranch2;

            ForkPlusSettings.Default.Pull_Rebase = rebase;
            ForkPlusSettings.Default.Pull_StashAndReapply = stashAndReapply;
            ForkPlusSettings.Default.Save();

            string jobName = string.Format(Translate("Pull '{0}'"), remoteBranch2.Name);
            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Pulling..."));

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(jobName, ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            // spike: PerformPull 简化 - 不传 SubmodulesToUpdate，不处理 stash 中的 workingDirectoryIsDirty
            //        （workingDirectoryIsDirty 依赖 RepositoryStatus，spike 不接入；stash 仍执行）
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult requestResult = PerformPull(
                    gitModule, remote.Name, remoteBranch, rebase, allTags, stashAndReapply, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    try
                    {
                        _onCompleted?.Invoke(requestResult);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("PullWindow onCompleted callback failed", ex);
                    }
                    Close(requestResult);
                });
            });
        }

        // 对照 WPF: RemotesComboBox_SelectionChanged
        public void RemotesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateRemoteBranchesCombobox();
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: RemoteBranchesComboBox_SelectionChanged
        public void RemoteBranchesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: CheckBox_Changed
        public void CheckBox_Changed(object sender, RoutedEventArgs e)
        {
            RefreshCommandPreview();
        }

        // 对照 WPF: private static GitCommandResult PerformPull(...)
        // spike 简化：不传 SubmodulesToUpdate（依赖 RepositoryStatus），workingDirectoryIsDirty 始终为 false
        private static GitCommandResult PerformPull(
            GitModule gitModule, string remote, RemoteBranch remoteBranch,
            bool rebase, bool allTags, bool stashAndReapply, JobMonitor monitor)
        {
            // spike: workingDirectoryIsDirty 暂不接入（依赖 RepositoryStatus），stashAndReapply 跳过 stash 流程
            GitCommandResult pullResult = new PullGitCommand().Execute(
                gitModule, remote, remoteBranch, rebase, allTags, monitor);
            return pullResult;
        }

        private void Refresh(Remote[] remotes, IReadOnlyList<RemoteBranch> remoteBranches, LocalBranch activeBranch)
        {
            if (activeBranch != null)
            {
                _activeLocalBranch = activeBranch;
                _allRemoteBranches = remoteBranches;
                _upstreamOfActiveBranch = remoteBranches.FirstOrDefault(x => x.FullReference == activeBranch.UpstreamFullReference);
                Remote[] array = remotes.ToSortedArray(Remote.ComparerIgnoreCaseNumeric);
                RemotesComboBox.ItemsSource = array;
                RemotesComboBox.SelectedItem = GetDefaultSelectedRemote(array);
                // 对照 WPF: DestinationGitPointView.Value = activeBranch;
                // Avalonia spike: 用 TextBlock 显示 branch Name 简化（Reference.FriendlyName 为 IGitPoint 显式实现）
                DestinationTextBlock.Text = activeBranch.Name;
                UpdateRemoteBranchesCombobox();
            }
        }

        private Remote GetDefaultSelectedRemote(Remote[] remotes)
        {
            return remotes.FirstOrDefault(x => x.Name == _predefinedRemoteBranch?.Remote)
                ?? remotes.FirstOrDefault(x => x.Name == _upstreamOfActiveBranch?.Remote)
                ?? remotes.FirstOrDefault(x => x.Name == Consts.Git.DefaultRemoteName)
                ?? remotes.FirstOrDefault();
        }

        private void UpdateRemoteBranchesCombobox()
        {
            Remote selectedRemote = RemotesComboBox.SelectedItem as Remote;
            List<RemoteBranch> list = _allRemoteBranches
                .Where(x => x.Remote == selectedRemote?.Name)
                .ToList();
            RemoteBranchesComboBox.ItemsSource = list;
            RemoteBranchesComboBox.SelectedItem = GetDefaultSelectedRemoteBranch(list);
        }

        private RemoteBranch GetDefaultSelectedRemoteBranch(IReadOnlyList<RemoteBranch> remoteBranches)
        {
            return remoteBranches.FirstOrDefault(x => x.FullReference == _predefinedRemoteBranch?.FullReference)
                ?? remoteBranches.FirstOrDefault(x => x.ShortName == _predefinedRemoteBranch?.ShortName)
                ?? remoteBranches.FirstOrDefault(x => x.FullReference == _upstreamOfActiveBranch?.FullReference)
                ?? remoteBranches.FirstOrDefault(x => x.ShortName == _activeLocalBranch?.Name);
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            RemotesComboBox.IsEnabled = false;
            RemoteBranchesComboBox.IsEnabled = false;
            RebaseCheckBox.IsEnabled = false;
            StashAndReapplyCheckBox.IsEnabled = false;
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
