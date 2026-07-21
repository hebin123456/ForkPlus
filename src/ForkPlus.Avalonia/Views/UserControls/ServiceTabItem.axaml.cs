using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Accounts;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 ServiceTabItem（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/ServiceTabItem.xaml.cs（59 行）：
    //   - TabItem 基类
    //   - 属性：RepositoryUserControl
    //   - 构造函数：InitializeComponent
    //   - Initialize(RepositoryUserControl)：注入父控件 + PullRequestsTabItem.Initialize +
    //     IssuesTabItem.Initialize
    //   - SetServices(Remote[])：PullRequestsTabItem.SetServices + IssuesTabItem.SetServices
    //     （IssuesTabItem 仅在 service 支持 issues 时显示）
    //   - TabControl_SelectionChanged：切到 PR/Issues tab 时触发 OnActivated
    //
    // Avalonia 版差异（spike 简化策略）：
    //   - WPF TabItem 基类 → Avalonia UserControl
    //   - WPF TabControl（嵌套 PR/Issues）→ Avalonia TabControl
    //   - WPF IssuesTabItem.Show/Collapse → IsVisible = true/false
    //   - WPF PreferencesLocalization → ServiceLocator.Localization
    //
    // spike 简化（task spec 关键 API）：
    //   - task spec 关键 API：Initialize(Account, Repository) / Refresh()
    //   - WPF Initialize(RepositoryUserControl) → spike Initialize(Account, Repository)
    //   - WPF SetServices(Remote[]) → spike SetServices(string[] remoteNames)
    //   - GitHub API 调用简化为占位
    //   - PullRequestsTabItem + IssuesTabItem 嵌入（spike 复用已迁移的子控件）
    public partial class ServiceTabItem : UserControl
    {
        // ===== 私有字段（对照 WPF）=====
        // 对照 WPF: public RepositoryUserControl RepositoryUserControl { get; private set; }
        private Account _account;
        private ForkPlusSettings.RepositoryManagerSettings.Repository _repository;

        // 对照 WPF: PullRequestsTabItem + IssuesTabItem（XAML 中声明）
        // spike 版：由 code-behind 动态创建并注入到 ContentControl
        private PullRequestsTabItem _pullRequestsTabItem;
        private IssuesTabItem _issuesTabItem;

        // ===== 注入回调（替代 MainWindow.Instance 依赖）=====
        // 对照 WPF: issue.OpenInBrowser() / pullRequest.OpenInBrowser()
        public Action<string> OpenInBrowserCallback { get; set; }

        // 对照 WPF: NewIssueButton_Click → service.GetNewIssueUrl
        public Action<Account, ForkPlusSettings.RepositoryManagerSettings.Repository> NewIssueCallback { get; set; }

        // 对照 WPF: NewPullRequestButton_Click → service.GetNewPullRequestUrl
        public Action<Account, ForkPlusSettings.RepositoryManagerSettings.Repository> NewPullRequestCallback { get; set; }

        // ===== 构造函数 =====
        public ServiceTabItem()
        {
            InitializeComponent();
            // spike 版：创建子控件实例（对照 WPF XAML 中声明的 PullRequestsTabItem + IssuesTabItem）
            _pullRequestsTabItem = new PullRequestsTabItem
            {
                OpenInBrowserCallback = url => OpenInBrowserCallback?.Invoke(url),
                NewPullRequestCallback = (a, r) => NewPullRequestCallback?.Invoke(a, r)
            };
            _issuesTabItem = new IssuesTabItem
            {
                OpenInBrowserCallback = url => OpenInBrowserCallback?.Invoke(url),
                NewIssueCallback = (a, r) => NewIssueCallback?.Invoke(a, r)
            };
            // 注入到 ContentControl
            if (PullRequestsContainer != null) PullRequestsContainer.Content = _pullRequestsTabItem;
            if (IssuesContainer != null) IssuesContainer.Content = _issuesTabItem;
        }

        // ===== Initialize(Account, Repository)（task spec 关键 API）=====
        // 对照 WPF: public void Initialize(RepositoryUserControl repositoryUserControl)
        //   WPF: RepositoryUserControl = repositoryUserControl;
        //         PullRequestsTabItem.Initialize(repositoryUserControl);
        //         IssuesTabItem.Initialize(repositoryUserControl);
        // spike 版：task spec 简化为 Initialize(Account, Repository)
        public void Initialize(Account account, ForkPlusSettings.RepositoryManagerSettings.Repository repository)
        {
            _account = account;
            _repository = repository;

            // 对照 WPF: PullRequestsTabItem.Initialize(repositoryUserControl)
            _pullRequestsTabItem?.Initialize(account, repository);
            // 对照 WPF: IssuesTabItem.Initialize(repositoryUserControl)
            _issuesTabItem?.Initialize(account, repository);

            if (FallbackPanel != null) FallbackPanel.IsVisible = false;
            if (ServiceTabControl != null) ServiceTabControl.IsVisible = true;
        }

        // ===== Refresh()（task spec 关键 API）=====
        // 对照 WPF: TabControl_SelectionChanged → pullRequestsTabItem.OnActivated() /
        //   issuesTabItem.OnActivated()
        // spike 版：刷新当前激活的子 tab
        public void Refresh()
        {
            if (_account == null || _repository == null)
            {
                if (FallbackPanel != null) FallbackPanel.IsVisible = true;
                if (ServiceTabControl != null) ServiceTabControl.IsVisible = false;
                return;
            }

            // 对照 WPF: pullRequestsTabItem.OnActivated() → RefreshIfNeeded() → Reset() + LoadNext()
            _pullRequestsTabItem?.Refresh();
            // 对照 WPF: issuesTabItem.OnActivated()
            _issuesTabItem?.Refresh();
        }

        // ===== SetServices（对照 WPF）=====
        // 对照 WPF: public void SetServices(Remote[] remotesWithService)
        //   WPF: PullRequestsTabItem.SetServices(remotesWithService);
        //         List<Remote> list = remotesWithService.Filter(x => x.AccountConcrete.Service.SupportsIssues);
        //         if (list.Count > 0) { IssuesTabItem.Show(); IssuesTabItem.SetServices(list.ToArray()); }
        //         else { IssuesTabItem.Collapse(); }
        // spike 版：用 string[] remoteNames 简化（真实 Remote 类型的 service 检查留待后续 Phase）
        public void SetServices(string[] remoteNames, bool supportsIssues = true)
        {
            // 对照 WPF: PullRequestsTabItem.SetServices(remotesWithService)
            _pullRequestsTabItem?.SetRemotes(remoteNames);

            // 对照 WPF: if (list.Count > 0) IssuesTabItem.Show() else IssuesTabItem.Collapse()
            if (supportsIssues && IssuesTab != null)
            {
                IssuesTab.IsVisible = true;
                _issuesTabItem?.SetRemotes(remoteNames);
            }
            else if (IssuesTab != null)
            {
                IssuesTab.IsVisible = false;
            }
        }

        // ===== TabControl_SelectionChanged（对照 WPF）=====
        // 对照 WPF: private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //   WPF: if (e.AddedItems.FirstItem<PullRequestsTabItem>() != null) pullRequestsTabItem.OnActivated();
        //        if (e.AddedItems.FirstItem<IssuesTabItem>() != null) issuesTabItem.OnActivated();
        // spike 版：切换 tab 时触发对应子控件的 Refresh
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ServiceTabControl?.SelectedItem == PullRequestsTab)
            {
                _pullRequestsTabItem?.Refresh();
            }
            else if (ServiceTabControl?.SelectedItem == IssuesTab)
            {
                _issuesTabItem?.Refresh();
            }
        }
    }
}
