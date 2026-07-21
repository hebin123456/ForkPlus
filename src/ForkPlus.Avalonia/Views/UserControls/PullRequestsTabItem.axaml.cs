using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Accounts;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 PullRequestsTabItem（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/PullRequestsTabItem.xaml.cs（398 行）：
    //   - TabItem 基类，含 MultiselectionTreeView + FilterTextBox + RemoteDropdownButton +
    //     BusyIndicator + RefreshButton + NewPullRequestButton + FallbackUserControl
    //   - Initialize(RepositoryUserControl) 注入父控件
    //   - SetServices(Remote[]) 选择默认 remote + 设置 FilterTextBox.Hint
    //   - Reset() + LoadNext() 分页加载 pull requests（IPaged<PullRequest>）
    //   - PullRequestButton_Click / NewPullRequestButton_Click / RefreshButton_Click 事件处理
    //   - SourceBranchButton_Click 选中 source branch 对应的 remote branch
    //   - FilterTextBox_DropdownContextMenuOpened 历史/筛选菜单
    //   - ScrollViewer_ScrollChanged 滚动到底自动加载下一页
    //
    // Avalonia 版差异（spike 简化策略）：
    //   - WPF TabItem 基类 → Avalonia UserControl
    //   - WPF MultiselectionTreeView → Avalonia ListBox
    //   - WPF FilterTextBox 自定义控件 → Avalonia TextBox
    //   - WPF RemoteDropdownButton → Avalonia ComboBox
    //   - WPF Image PNG → TextBlock emoji
    //   - WPF Dispatcher.Async → Dispatcher.UIThread.Post
    //   - WPF Visibility.Collapsed/Visible → IsVisible = false/true
    //   - WPF PreferencesLocalization → ServiceLocator.Localization
    //   - WPF MouseDoubleClick → DoubleTapped
    //
    // spike 简化：
    //   - task spec 关键 API：Initialize(Account, Repository) / Refresh()
    //   - WPF Initialize(RepositoryUserControl) → spike Initialize(Account, Repository)
    //   - account.Service.GetPullRequests() 真实调用留待后续 Phase，spike 版用
    //     SetPullRequests(IEnumerable<PullRequest>) 公共方法
    //   - SourceBranchButton_Click 选中 remote branch → spike 不实现（依赖 RepositoryUserControl）
    //   - IPaged<PullRequest> 分页加载 → spike 一次性加载全部
    //   - JobQueue → spike 不使用
    public partial class PullRequestsTabItem : UserControl
    {
        // ===== 内部 POCO ViewModel（对照 WPF PullRequestItem）=====
        public class PullRequestViewModel
        {
            public string Id { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string SourceBranch { get; set; }
            public string AuthorName { get; set; }
            public PullRequestState State { get; set; }
            public string WebUrl { get; set; } = string.Empty;

            // spike 绑定辅助属性
            // 对照 WPF PullRequestStateToColorConverter
            public string StateEmoji => State switch
            {
                PullRequestState.Open => "🟢",
                PullRequestState.Closed => "🔴",
                PullRequestState.Merged => "🟣",
                _ => "⚪"
            };

            public string IdLine => "#" + Id + (string.IsNullOrEmpty(AuthorName) ? "" : "  ·  @" + AuthorName);
            public bool HasSourceBranch => !string.IsNullOrEmpty(SourceBranch);
            public string SourceBranchEmoji => "🌿 " + (SourceBranch ?? string.Empty);

            public PullRequestViewModel() { }

            public PullRequestViewModel(PullRequest pr)
            {
                Id = pr?.Id ?? string.Empty;
                Title = pr?.Title ?? string.Empty;
                SourceBranch = pr?.SourceBranch;
                AuthorName = pr?.AuthorName;
                State = pr?.State ?? PullRequestState.Open;
                WebUrl = pr?.WebUrl ?? string.Empty;
            }

            // 对照 WPF PullRequestItem.OpenInBrowser()
            public void OpenInBrowser(Action<string> openInBrowser)
            {
                if (!string.IsNullOrEmpty(WebUrl)) openInBrowser?.Invoke(WebUrl);
            }
        }

        // ===== 私有字段（对照 WPF）=====
        private Account _account;
        private ForkPlusSettings.RepositoryManagerSettings.Repository _repository;
        private PullRequestViewModel[] _allPrs = Array.Empty<PullRequestViewModel>();
        private string _searchQuery = string.Empty;

        // ===== 注入回调（替代 MainWindow.Instance 依赖）=====
        public Action<string> OpenInBrowserCallback { get; set; }

        // 对照 WPF: NewPullRequestButton_Click → service.GetNewPullRequestUrl(url).OpenInBrowser()
        public Action<Account, ForkPlusSettings.RepositoryManagerSettings.Repository> NewPullRequestCallback { get; set; }

        public Account Account => _account;
        public ForkPlusSettings.RepositoryManagerSettings.Repository Repository => _repository;

        // ===== 构造函数（task spec spike 签名）=====
        public PullRequestsTabItem()
        {
            InitializeComponent();
            ShowFallback(Translate("No pull requests loaded"), Translate("Select a repository and click Refresh"));
        }

        // ===== Initialize(Account, Repository)（task spec 关键 API）=====
        // 对照 WPF: public void Initialize(RepositoryUserControl repositoryUserControl)
        // spike 版：task spec 简化为 Initialize(Account, Repository)
        public void Initialize(Account account, ForkPlusSettings.RepositoryManagerSettings.Repository repository)
        {
            _account = account;
            _repository = repository;
            ShowFallback(Translate("Ready to load"), Translate("Click Refresh to load pull requests"));
        }

        // ===== Refresh()（task spec 关键 API）=====
        // 对照 WPF: RefreshButton_Click → Reset() + LoadNext()
        // spike 版：显示 loading，等待外部 SetPullRequests 注入数据
        public void Refresh()
        {
            if (_account == null || _repository == null)
            {
                ShowFallback(Translate("Not initialized"), Translate("Call Initialize(account, repository) first"));
                return;
            }

            BusyIndicator.IsVisible = true;
            RefreshButton.IsVisible = false;
            ShowFallback(Translate("Loading pull requests..."), null);

            // spike 版不调 account.Service.GetPullRequests()，等待外部 SetPullRequests
            Dispatcher.UIThread.Post(() =>
            {
                if (_allPrs.Length == 0)
                {
                    BusyIndicator.IsVisible = false;
                    RefreshButton.IsVisible = true;
                    ShowFallback(Translate("No pull requests"), Translate("GitHub API call placeholder - inject via SetPullRequests()"));
                }
            });
        }

        // ===== SetPullRequests（spike 新增，替代 WPF JobQueue + GetPullRequests 异步流程）=====
        public void SetPullRequests(IEnumerable<PullRequest> pullRequests)
        {
            _allPrs = (pullRequests ?? Array.Empty<PullRequest>())
                .Select(x => new PullRequestViewModel(x))
                .ToArray();
            BusyIndicator.IsVisible = false;
            RefreshButton.IsVisible = true;
            UpdateList(_searchQuery);
        }

        // ===== SetError（spike 新增，替代 WPF FallbackUserControl 错误显示）=====
        public void SetError(string message)
        {
            BusyIndicator.IsVisible = false;
            RefreshButton.IsVisible = true;
            ShowFallback(Translate("Error"), message);
            PullRequestsListBox.ItemsSource = null;
            ResultCountTextBlock.Text = string.Empty;
        }

        // ===== SetRemotes（spike 新增，替代 WPF SetServices + RemoteDropdownButton）=====
        // 对照 WPF: public void SetServices(Remote[] remotesWithService)
        public void SetRemotes(string[] remoteNames, string defaultRemote = null)
        {
            if (remoteNames == null || remoteNames.Length == 0) return;

            if (remoteNames.Length > 1)
            {
                RemoteDropdownButton.IsVisible = true;
                RemoteDropdownButton.ItemsSource = remoteNames;
                if (!string.IsNullOrEmpty(defaultRemote))
                {
                    RemoteDropdownButton.SelectedItem = defaultRemote;
                }
            }
            else
            {
                RemoteDropdownButton.IsVisible = false;
            }
        }

        // ===== 事件处理（对照 WPF）=====

        // 对照 WPF: private void RefreshButton_Click(object sender, RoutedEventArgs e)
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        // 对照 WPF: private void NewPullRequestButton_Click(object sender, RoutedEventArgs e)
        private void NewPullRequestButton_Click(object sender, RoutedEventArgs e)
        {
            NewPullRequestCallback?.Invoke(_account, _repository);
        }

        // 对照 WPF: private void PullRequestButton_Click(object sender, RoutedEventArgs e)
        //   WPF: pullRequestItem.OpenInBrowser()
        // spike 版: 双击 ListBox 项打开浏览器
        private void PullRequestItem_DoubleTapped(object sender, RoutedEventArgs e)
        {
            if (PullRequestsListBox.SelectedItem is PullRequestViewModel pr)
            {
                pr.OpenInBrowser(OpenInBrowserCallback);
            }
        }

        // 对照 WPF: FilterTextBox.KeyDown (Key.Return) → Reset() + LoadNext()
        private void FilterTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                _searchQuery = FilterTextBox?.Text ?? string.Empty;
                UpdateList(_searchQuery);
                e.Handled = true;
            }
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchQuery = FilterTextBox?.Text ?? string.Empty;
            UpdateList(_searchQuery);
        }

        private void RemoteDropdownButton_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RemoteDropdownButton.SelectedItem != null && _account != null)
            {
                Refresh();
            }
        }

        // ===== 私有辅助方法 =====

        private void UpdateList(string filterString)
        {
            if (_allPrs.Length == 0)
            {
                PullRequestsListBox.ItemsSource = null;
                ResultCountTextBlock.Text = string.Empty;
                return;
            }

            string lower = (filterString ?? string.Empty).ToLowerInvariant();
            IEnumerable<PullRequestViewModel> filtered = string.IsNullOrEmpty(lower)
                ? _allPrs
                : _allPrs.Where(x => (x.Title ?? string.Empty).ToLowerInvariant().Contains(lower)
                                  || (x.Id ?? string.Empty).ToLowerInvariant().Contains(lower)
                                  || (x.SourceBranch ?? string.Empty).ToLowerInvariant().Contains(lower));

            PullRequestViewModel[] items = filtered.ToArray();
            PullRequestsListBox.ItemsSource = items;
            ResultCountTextBlock.Text = string.Format(Translate("{0} results"), items.Length);
            HideFallback();
        }

        private void ShowFallback(string title, string message)
        {
            if (FallbackPanel != null) FallbackPanel.IsVisible = true;
            if (FallbackTitle != null)
            {
                FallbackTitle.Text = title ?? string.Empty;
                FallbackTitle.IsVisible = !string.IsNullOrEmpty(title);
            }
            if (FallbackMessage != null) FallbackMessage.Text = message ?? string.Empty;
            if (PullRequestsListBox != null) PullRequestsListBox.IsVisible = false;
        }

        private void HideFallback()
        {
            if (FallbackPanel != null) FallbackPanel.IsVisible = false;
            if (PullRequestsListBox != null) PullRequestsListBox.IsVisible = true;
        }

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
