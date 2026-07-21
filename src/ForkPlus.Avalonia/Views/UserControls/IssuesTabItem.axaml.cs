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
    // Avalonia 版 IssuesTabItem（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/IssuesTabItem.xaml.cs（374 行）：
    //   - TabItem 基类，含 MultiselectionTreeView + FilterTextBox + RemoteDropdownButton +
    //     BusyIndicator + RefreshButton + NewIssueButton + FallbackUserControl
    //   - Initialize(RepositoryUserControl) 注入父控件
    //   - SetServices(Remote[]) 选择默认 remote + 设置 FilterTextBox.Hint
    //   - Reset() + LoadNext() 分页加载 issues（IPaged<Issue>）
    //   - IssueButton_Click / NewIssueButton_Click / RefreshButton_Click 事件处理
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
    //
    // spike 简化：
    //   - task spec 关键 API：Initialize(Account, Repository) / Refresh()
    //   - WPF Initialize(RepositoryUserControl) → spike Initialize(Account, Repository)
    //     （task spec 简化策略 #3：Account / Repository 类型直接使用）
    //   - account.Service.GetIssues() 真实调用留待后续 Phase，spike 版用
    //     SetIssues(IEnumerable<Issue>) 公共方法允许外部直接注入数据
    //   - IPaged<Issue> 分页加载 → spike 一次性加载全部
    //   - JobQueue → spike 不使用
    //   - ScrollViewer 滚动加载 → spike 不实现
    public partial class IssuesTabItem : UserControl
    {
        // ===== 内部 POCO ViewModel（对照 WPF IssueItem）=====
        public class IssueViewModel
        {
            public string Id { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string AssigneeName { get; set; }
            public IssueState State { get; set; }
            public string WebUrl { get; set; } = string.Empty;

            // spike 绑定辅助属性
            public string StateEmoji => State == IssueState.Open ? "🟢" : "🔴";
            public string IdLine => "#" + Id + (string.IsNullOrEmpty(AssigneeName) ? "" : "  ·  @" + AssigneeName);
            public bool HasAssignee => !string.IsNullOrEmpty(AssigneeName);

            public IssueViewModel() { }

            public IssueViewModel(Issue issue)
            {
                Id = issue?.Id ?? string.Empty;
                Title = issue?.Title ?? string.Empty;
                AssigneeName = issue?.AssigneeName;
                State = issue?.State ?? IssueState.Open;
                WebUrl = issue?.WebUrl ?? string.Empty;
            }

            // 对照 WPF IssueItem.OpenInBrowser()
            public void OpenInBrowser(Action<string> openInBrowser)
            {
                if (!string.IsNullOrEmpty(WebUrl)) openInBrowser?.Invoke(WebUrl);
            }
        }

        // ===== 私有字段（对照 WPF）=====
        private Account _account;
        private ForkPlusSettings.RepositoryManagerSettings.Repository _repository;
        private IssueViewModel[] _allIssues = Array.Empty<IssueViewModel>();
        private string _searchQuery = string.Empty;

        // ===== 注入回调（替代 MainWindow.Instance 依赖）=====
        // 对照 WPF: issue.OpenInBrowser() / new Uri(url).OpenInBrowser()
        public Action<string> OpenInBrowserCallback { get; set; }

        // 对照 WPF: NewIssueButton_Click → service.GetNewIssueUrl(url).OpenInBrowser()
        public Action<Account, ForkPlusSettings.RepositoryManagerSettings.Repository> NewIssueCallback { get; set; }

        // 对照 WPF: RepositoryUserControl 属性
        public Account Account => _account;
        public ForkPlusSettings.RepositoryManagerSettings.Repository Repository => _repository;

        // ===== 构造函数（task spec spike 签名）=====
        public IssuesTabItem()
        {
            InitializeComponent();
            ShowFallback(Translate("No issues loaded"), Translate("Select a repository and click Refresh"));
        }

        // ===== Initialize(Account, Repository)（task spec 关键 API）=====
        // 对照 WPF: public void Initialize(RepositoryUserControl repositoryUserControl)
        //   WPF: RepositoryUserControl = repositoryUserControl;
        // spike 版：task spec 简化为 Initialize(Account, Repository)
        public void Initialize(Account account, ForkPlusSettings.RepositoryManagerSettings.Repository repository)
        {
            _account = account;
            _repository = repository;
            ShowFallback(Translate("Ready to load"), Translate("Click Refresh to load issues"));
        }

        // ===== Refresh()（task spec 关键 API）=====
        // 对照 WPF: RefreshButton_Click → Reset() + LoadNext()
        // spike 版：显示 loading，等待外部 SetIssues 注入数据
        public void Refresh()
        {
            if (_account == null || _repository == null)
            {
                ShowFallback(Translate("Not initialized"), Translate("Call Initialize(account, repository) first"));
                return;
            }

            // 对照 WPF: BusyIndicator.Show() / RefreshButton.Hide()
            BusyIndicator.IsVisible = true;
            RefreshButton.IsVisible = false;
            ShowFallback(Translate("Loading issues..."), null);

            // spike 版不调 account.Service.GetIssues()，等待外部 SetIssues
            // 真实网络调用留待后续 Phase
            Dispatcher.UIThread.Post(() =>
            {
                if (_allIssues.Length == 0)
                {
                    BusyIndicator.IsVisible = false;
                    RefreshButton.IsVisible = true;
                    ShowFallback(Translate("No issues"), Translate("GitHub API call placeholder - inject via SetIssues()"));
                }
            });
        }

        // ===== SetIssues（spike 新增，替代 WPF JobQueue + GetIssues 异步流程）=====
        // 调用方完成 Git 服务请求后通过此方法注入 issues 列表
        public void SetIssues(IEnumerable<Issue> issues)
        {
            _allIssues = (issues ?? Array.Empty<Issue>())
                .Select(x => new IssueViewModel(x))
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
            IssuesListBox.ItemsSource = null;
            ResultCountTextBlock.Text = string.Empty;
        }

        // ===== SetRemotes（spike 新增，替代 WPF SetServices + RemoteDropdownButton）=====
        // 对照 WPF: public void SetServices(Remote[] remotesWithService)
        // spike 版：用 string[] 简化（remote name），多 remote 时显示 ComboBox
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

        // 对照 WPF: private void NewIssueButton_Click(object sender, RoutedEventArgs e)
        //   WPF: service.GetNewIssueUrl(_selectedRemote).OpenInBrowser()
        // spike 版: 调用注入的 NewIssueCallback
        private void NewIssueButton_Click(object sender, RoutedEventArgs e)
        {
            NewIssueCallback?.Invoke(_account, _repository);
        }

        // 对照 WPF: private void IssueButton_Click(object sender, RoutedEventArgs e)
        //   WPF: issueItem.OpenInBrowser()
        // spike 版: 双击 ListBox 项打开浏览器（DoubleTapped 替代 Click）
        private void IssueItem_DoubleTapped(object sender, RoutedEventArgs e)
        {
            if (IssuesListBox.SelectedItem is IssueViewModel issue)
            {
                issue.OpenInBrowser(OpenInBrowserCallback);
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

        // 对照 WPF: FilterTextBox.TextChanged → 过滤
        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchQuery = FilterTextBox?.Text ?? string.Empty;
            UpdateList(_searchQuery);
        }

        // 对照 WPF: RemoteDropdownButtonContextMenu_Opened → 切换 remote
        private void RemoteDropdownButton_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // spike 版：切换 remote 时触发 Refresh（真实 remote 切换留待后续 Phase）
            if (RemoteDropdownButton.SelectedItem != null && _account != null)
            {
                Refresh();
            }
        }

        // ===== 私有辅助方法 =====

        // 对照 WPF: Reset() 后 _root.Children.Clear() + 重新过滤
        private void UpdateList(string filterString)
        {
            if (_allIssues.Length == 0)
            {
                IssuesListBox.ItemsSource = null;
                ResultCountTextBlock.Text = string.Empty;
                return;
            }

            string lower = (filterString ?? string.Empty).ToLowerInvariant();
            IEnumerable<IssueViewModel> filtered = string.IsNullOrEmpty(lower)
                ? _allIssues
                : _allIssues.Where(x => (x.Title ?? string.Empty).ToLowerInvariant().Contains(lower)
                                      || (x.Id ?? string.Empty).ToLowerInvariant().Contains(lower));

            IssueViewModel[] items = filtered.ToArray();
            IssuesListBox.ItemsSource = items;
            ResultCountTextBlock.Text = string.Format(Translate("{0} results"), items.Length);
            HideFallback();
        }

        // 对照 WPF: FallbackUserControl.Show/Hide
        private void ShowFallback(string title, string message)
        {
            if (FallbackPanel != null) FallbackPanel.IsVisible = true;
            if (FallbackTitle != null)
            {
                FallbackTitle.Text = title ?? string.Empty;
                FallbackTitle.IsVisible = !string.IsNullOrEmpty(title);
            }
            if (FallbackMessage != null) FallbackMessage.Text = message ?? string.Empty;
            if (IssuesListBox != null) IssuesListBox.IsVisible = false;
        }

        private void HideFallback()
        {
            if (FallbackPanel != null) FallbackPanel.IsVisible = false;
            if (IssuesListBox != null) IssuesListBox.IsVisible = true;
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
