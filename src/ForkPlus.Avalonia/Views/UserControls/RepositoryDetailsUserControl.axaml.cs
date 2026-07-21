using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 RepositoryDetailsUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/RepositoryDetailsUserControl.xaml.cs（393 行）：
    //   - 嵌套类：RemoteViewModel / ReferenceViewModel / RevisionViewModel（INPC）
    //   - 字段：_updatePreviewAction (DelayedAction) / _selectedRepository
    //   - 属性：RepositoryManagerUserControl
    //   - 构造函数：InitializeComponent + _updatePreviewAction + ApplyLocalization
    //   - ShowDetails(Repository?)：触发延迟更新预览
    //   - RefreshRepositoryName()：更新 RepositoryName.Text
    //   - UpdatePreview(Repository?)：异步执行多个 git 命令填充统计字段 +
    //     Remotes/Branches/Tags/Commits ItemsControl + ReadmeTextBox
    //   - ShowGitMmDetails(Repository)：git mm workspace 特殊处理
    //   - 多个事件处理：OpenRepositoryButton_Click / Hyperlink_RequestNavigate /
    //     FallbackUserControl_Button1Click / UnsafeRepositoryFallbackUserControl_Button1Click /
    //     ModernTabControl_SelectionChanged / OpeneInFileExplorerButton_Click
    //
    // Avalonia 版差异（spike 简化策略）：
    //   - WPF UserControl + ILocalizableControl → Avalonia UserControl
    //   - WPF DelayedAction → spike 直接调用
    //   - WPF Dispatcher.Async → Dispatcher.UIThread.Post
    //   - WPF Visibility.Collapsed/Visible → IsVisible = false/true
    //   - WPF PreferencesLocalization → ServiceLocator.Localization
    //   - WPF Hyperlink.RequestNavigate → 不迁移（spike 简化）
    //   - WPF ModernTabControl → Avalonia TabControl
    //   - WPF FallbackUserControl 三种 → spike 统一 FallbackPanel
    //
    // spike 简化（task spec 关键 API）：
    //   - task spec 关键 API：Initialize(RepositoryUserControl) / Refresh() / SetRepository(Repository?)
    //   - WPF ShowDetails(Repository?) → spike SetRepository(Repository?)
    //   - WPF UpdatePreview 多个 git 命令 → spike 占位（需 GitModule，真实调用留待后续 Phase）
    //   - git mm workspace 特殊处理 → spike 不实现
    //   - Remotes/Branches/Tags/Commits → spike POCO + 公共 Set* 方法注入数据
    //   - ReadmeTextBox → spike 不实现
    //   - 仓库操作回调注入（替代 MainWindow.Instance 依赖）
    public partial class RepositoryDetailsUserControl : UserControl
    {
        // ===== 嵌套 POCO ViewModels（对照 WPF RemoteViewModel / ReferenceViewModel / RevisionViewModel）=====

        // 对照 WPF: public class RemoteViewModel : INotifyPropertyChanged
        public class RemoteViewModel : INotifyPropertyChanged
        {
            public string Name { get; set; } = string.Empty;
            public string WebsiteName { get; set; } = string.Empty;
            public string WebsiteUrl { get; set; } = string.Empty;
            public string IssuesUrl { get; set; } = string.Empty;
            public string PullRequestsUrl { get; set; } = string.Empty;
            // 对照 WPF: Visibility → spike bool (IsVisible)
            public bool IsWebsiteVisible => !string.IsNullOrEmpty(WebsiteName);
            public bool IsIssuesVisible => !string.IsNullOrEmpty(IssuesUrl);
            public bool IsPullRequestsVisible => !string.IsNullOrEmpty(PullRequestsUrl);

            public event PropertyChangedEventHandler PropertyChanged { add { } remove { } }

            public RemoteViewModel() { }

            public RemoteViewModel(Remote remote)
            {
                Name = remote?.Name ?? string.Empty;
                // 对照 WPF: RepositoryUrlBuilder(remote).RepositoryWebpageUrl
                // spike 简化：仅显示 remote name + url（真实 RepositoryUrlBuilder 留待后续 Phase）
                WebsiteName = remote?.RemoteType.FriendlyName() ?? string.Empty;
                WebsiteUrl = remote?.Url ?? string.Empty;
            }
        }

        // 对照 WPF: public class ReferenceViewModel : INotifyPropertyChanged
        public class ReferenceViewModel : INotifyPropertyChanged
        {
            public string Name { get; set; } = string.Empty;
            public string CommitterName { get; set; } = string.Empty;
            public string RevisionSubject { get; set; } = string.Empty;
            public string RelativeDate { get; set; } = string.Empty;

            public event PropertyChangedEventHandler PropertyChanged { add { } remove { } }

            public ReferenceViewModel() { }

            public ReferenceViewModel(string name, string committerName, string revisionSubject, string relativeDate)
            {
                Name = name;
                CommitterName = committerName;
                RevisionSubject = revisionSubject;
                RelativeDate = relativeDate;
            }
        }

        // 对照 WPF: public class RevisionViewModel : INotifyPropertyChanged
        public class RevisionViewModel : INotifyPropertyChanged
        {
            public string AuthorName { get; set; } = string.Empty;
            public string RevisionSubject { get; set; } = string.Empty;
            public string RelativeDate { get; set; } = string.Empty;

            public event PropertyChangedEventHandler PropertyChanged { add { } remove { } }

            public RevisionViewModel() { }

            public RevisionViewModel(string authorName, string revisionSubject, string relativeDate)
            {
                AuthorName = authorName;
                RevisionSubject = revisionSubject;
                RelativeDate = relativeDate;
            }
        }

        // ===== 私有字段（对照 WPF）=====
        private ForkPlusSettings.RepositoryManagerSettings.Repository? _selectedRepository;

        // 对照 WPF: public RepositoryManagerUserControl RepositoryManagerUserControl { get; set; }
        // spike 版：父控件引用（task spec 用 object 占位，对照 spike 策略 #1）
        public object RepositoryManagerUserControl { get; set; }

        // ===== 注入回调（替代 MainWindow.Instance / RepositoryManager.Instance 依赖）=====
        // 对照 WPF: OpenRepositoryButton_Click → Application.Current.TabManager()?.OpenRepository(path)
        public Action<string> OpenRepositoryCallback { get; set; }

        // 对照 WPF: OpeneInFileExplorerButton_Click → MainWindow.Commands.OpenRepositoryInFileExplorer
        public Action<string> OpenInFileExplorerCallback { get; set; }

        // 对照 WPF: FallbackUserControl_Button1Click → RepositoryManager.Instance.DeleteRepositories
        public Action<string> DeleteRepositoryCallback { get; set; }

        // ===== 构造函数（task spec spike 签名）=====
        public RepositoryDetailsUserControl()
        {
            InitializeComponent();
            ShowFallback(Translate("No repository selected"), Translate("Select a repository from the left panel"));
        }

        // ===== Initialize(RepositoryUserControl)（task spec 关键 API）=====
        // 对照 WPF: public RepositoryManagerUserControl RepositoryManagerUserControl { get; set; }
        // spike 版：task spec 关键 API，注入父控件（spike 用 object 占位）
        public void Initialize(object repositoryManagerUserControl)
        {
            RepositoryManagerUserControl = repositoryManagerUserControl;
        }

        // ===== SetRepository(Repository?)（task spec 关键 API）=====
        // 对照 WPF: public void ShowDetails(Repository? repository)
        //   WPF: _selectedRepository = repository; _updatePreviewAction.InvokeWithDelay(repository);
        // spike 版：直接调用 UpdatePreview（无 DelayedAction）
        public void SetRepository(ForkPlusSettings.RepositoryManagerSettings.Repository? repository)
        {
            _selectedRepository = repository;
            UpdatePreview(repository);
        }

        // ===== Refresh()（task spec 关键 API）=====
        // 对照 WPF: RefreshRepositoryName() + 重新触发 UpdatePreview
        // spike 版：重新加载当前选中仓库的预览
        public void Refresh()
        {
            RefreshRepositoryName();
            if (_selectedRepository != null)
            {
                UpdatePreview(_selectedRepository);
            }
        }

        // 对照 WPF: public void RefreshRepositoryName()
        //   WPF: RepositoryName.Text = GitMmUserControl.IsGitMmWorkspace(path) ? "git mm: " + repo.Name() : repo.Name()
        // spike 版: repository.Name 属性（ForkPlusSettings.RepositoryManagerSettings.Repository，
        //   WPF 的 Name() 是 extension method，spike 用属性）
        public void RefreshRepositoryName()
        {
            if (_selectedRepository != null && RepositoryName != null)
            {
                var repo = _selectedRepository;
                RepositoryName.Text = repo.Name ?? string.Empty;
                if (RepositoryPath != null) RepositoryPath.Text = repo.Path;
            }
        }

        // 对照 WPF: private async void UpdatePreview(Repository? repo)
        // spike 版：仅显示基本信息，git 命令调用留待后续 Phase
        private void UpdatePreview(ForkPlusSettings.RepositoryManagerSettings.Repository? repo)
        {
            if (repo == null)
            {
                ShowFallback(Translate("No repository selected"), Translate("Select a repository from the left panel"));
                return;
            }

            var repository = repo;
            HideFallback();

            // 对照 WPF: RepositoryName.Text = repository.Name()
            // spike 版: repository.Name 属性（WPF Name() 是 extension method）
            if (RepositoryName != null) RepositoryName.Text = repository.Name ?? string.Empty;
            if (RepositoryPath != null) RepositoryPath.Text = repository.Path;

            // 对照 WPF: ChangedFilesTextBlock.Text = changedFilesCountResult.Result.ToString()
            // spike 版：占位（真实 git 命令调用留待后续 Phase）
            if (ChangedFilesTextBlock != null) ChangedFilesTextBlock.Text = "-";
            if (CommitsTextBlock != null) CommitsTextBlock.Text = "-";
            if (InitialCommitDateTextBlock != null) InitialCommitDateTextBlock.Text = "-";
            if (LastCommitDateTextBlock != null) LastCommitDateTextBlock.Text = "-";

            // spike 版：清空 ItemsControl（真实数据由调用方通过 Set* 方法注入）
            if (RemotesItemsControl != null) RemotesItemsControl.ItemsSource = null;
            if (BranchesItemsControl != null) BranchesItemsControl.ItemsSource = null;
            if (TagsItemsControl != null) TagsItemsControl.ItemsSource = null;
            if (CommitsItemsControl != null) CommitsItemsControl.ItemsSource = null;

            // 对照 WPF: if (StatisticsTabItem.IsSelected) StatisticsUserControl.ShowStatistics(gitModule);
            // spike 版：StatisticsUserControl 已嵌入，由 SelectionChanged 触发
        }

        // ===== Set* 方法（spike 新增，替代 WPF UpdatePreview 中的 git 命令调用）=====

        // 对照 WPF: RemotesItemsControl.ItemsSource = gitCommandResult2.Result.Items.Map(RemoteViewModel)
        public void SetRemotes(IEnumerable<Remote> remotes)
        {
            if (RemotesItemsControl == null) return;
            RemotesItemsControl.ItemsSource = (remotes ?? Array.Empty<Remote>())
                .Select(x => new RemoteViewModel(x))
                .ToArray();
        }

        // 对照 WPF: BranchesItemsControl.ItemsSource = result.RemoteBranches.Map(ReferenceViewModel)
        public void SetBranches(IEnumerable<ReferenceViewModel> branches)
        {
            if (BranchesItemsControl == null) return;
            BranchesItemsControl.ItemsSource = branches ?? Array.Empty<ReferenceViewModel>();
        }

        // 对照 WPF: TagsItemsControl.ItemsSource = result.Tags.Map(ReferenceViewModel)
        public void SetTags(IEnumerable<ReferenceViewModel> tags)
        {
            if (TagsItemsControl == null) return;
            TagsItemsControl.ItemsSource = tags ?? Array.Empty<ReferenceViewModel>();
        }

        // 对照 WPF: CommitsItemsControl.ItemsSource = result.Map(RevisionViewModel)
        public void SetCommits(IEnumerable<RevisionViewModel> commits)
        {
            if (CommitsItemsControl == null) return;
            CommitsItemsControl.ItemsSource = commits ?? Array.Empty<RevisionViewModel>();
        }

        // 对照 WPF: ChangedFiles / Commits / Initial / Last commit dates
        public void SetStatistics(int changedFiles, int commits, string initialDate, string lastDate)
        {
            if (ChangedFilesTextBlock != null) ChangedFilesTextBlock.Text = changedFiles.ToString();
            if (CommitsTextBlock != null) CommitsTextBlock.Text = commits.ToString();
            if (InitialCommitDateTextBlock != null) InitialCommitDateTextBlock.Text = initialDate ?? "-";
            if (LastCommitDateTextBlock != null) LastCommitDateTextBlock.Text = lastDate ?? "-";
        }

        // ===== 事件处理（对照 WPF）=====

        // 对照 WPF: private void OpenRepositoryButton_Click(object sender, RoutedEventArgs e)
        //   WPF: Application.Current.TabManager()?.OpenRepository(valueOrDefault.Path)
        // spike 版: 调用注入的 OpenRepositoryCallback
        private void OpenRepositoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRepository != null)
            {
                OpenRepositoryCallback?.Invoke(_selectedRepository.Path);
            }
        }

        // 对照 WPF: private void OpeneInFileExplorerButton_Click(object sender, RoutedEventArgs e)
        //   WPF: MainWindow.Commands.OpenRepositoryInFileExplorer.Execute(valueOrDefault.Path)
        // spike 版: 调用注入的 OpenInFileExplorerCallback
        private void OpenInFileExplorerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRepository != null)
            {
                OpenInFileExplorerCallback?.Invoke(_selectedRepository.Path);
            }
        }

        // 对照 WPF: private void ModernTabControl_SelectionChanged(...)
        //   WPF: if (StatisticsTabItem.IsSelected) StatisticsUserControl.ShowStatistics(gitModule);
        // spike 版：切换到 Statistics tab 时显示占位（真实统计留待后续 Phase）
        private void ModernTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StatisticsTabItem != null && StatisticsTabItem.IsSelected && _selectedRepository != null)
            {
                // 对照 WPF: StatisticsUserControl.ShowStatistics(gitModule)
                // spike 版：占位（真实 ShowStatistics 留待后续 Phase 接入 GitModule）
            }
        }

        // spike 新增：Refresh 按钮点击
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        // ===== 私有辅助方法（对照 WPF FallbackUserControl.Show/Hide）=====
        private void ShowFallback(string title, string message)
        {
            if (FallbackPanel != null) FallbackPanel.IsVisible = true;
            if (ModernTabControl != null) ModernTabControl.IsVisible = false;
            if (FallbackTitle != null) FallbackTitle.Text = title ?? string.Empty;
            if (FallbackMessage != null) FallbackMessage.Text = message ?? string.Empty;
        }

        private void HideFallback()
        {
            if (FallbackPanel != null) FallbackPanel.IsVisible = false;
            if (ModernTabControl != null) ModernTabControl.IsVisible = true;
        }

        // 对照 WPF: private static string Translate(string text)
        //   WPF: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
        //   spike: ServiceLocator.Localization.Translate(text, lang)
        private static string Translate(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (ServiceLocator.Localization == null) return text;
            try
            {
                return ServiceLocator.Localization.Translate(text, ForkPlusSettings.Default.UiLanguage);
            }
            catch
            {
                return text;
            }
        }
    }
}
