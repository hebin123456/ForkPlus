using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Accounts;
using ForkPlus.Git;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 AccountRepositoriesTabItem（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/AccountRepositoriesTabItem.xaml.cs（106 行）：
    //   - JobQueue 异步加载仓库列表（account.Service.GetRepositories() → _repositories + UpdateList）
    //   - DelayedAction<string> filter 延迟（FilterTextBox.FilterRequestChanged → UpdateList(filter)）
    //   - UpdateList(filter)：按 filter 字符串过滤 + GetAccountItems(repositories, icon)
    //   - GetAccountItems：按 Owner 分组，每组先 AccountHeaderItem 后 AccountRepositoryItem
    //   - CloneButton_Click：MainWindow.Commands.ShowCloneWindow.Execute(repo.GitHttpsUrl, _account)
    //   - Translate(text)：PreferencesLocalization.Translate
    //
    // Avalonia 版差异（spike 简化）：
    //   - WPF TabItem 基类 → UserControl
    //   - WPF JobQueue + account.Service.GetRepositories() → spike SetRepositories 公共方法
    //     （真实网络调用留待后续 Phase）
    //   - WPF DelayedAction → 直接过滤（TextChanged 立即触发）
    //   - WPF AccountItem / AccountHeaderItem / AccountRepositoryItem → spike 内部 ViewModel
    //   - WPF FilterTextBox.FilterRequest → TextBox.Text
    //   - WPF MainWindow.Commands.ShowCloneWindow → onClone 回调注入
    //   - WPF FallbackUserControl.Show/Collapse → Border.IsVisible = true/false
    public partial class AccountRepositoriesTabItem : UserControl
    {
        // ===== 内部 POCO ViewModels（对照 WPF AccountHeaderItem / AccountRepositoryItem / AccountItem）=====

        // 基类 POCO（对照 WPF AccountItem）
        public class AccountItem
        {
            public string Title { get; set; }
            public string Tooltip { get; set; }
            public bool IsRepository { get; set; }
            public GitServiceRepository Repository { get; set; }
        }

        // AccountHeaderItem：按 Owner 分组的组标题（对照 WPF AccountHeaderItem.cs）
        public class AccountHeaderItem : AccountItem
        {
            public AccountHeaderItem(string owner)
            {
                Title = owner;
                IsRepository = false;
            }
        }

        // AccountRepositoryItem：单个仓库项（对照 WPF AccountRepositoryItem.cs）
        public class AccountRepositoryItem : AccountItem
        {
            public AccountRepositoryItem(GitServiceRepository repository)
            {
                Repository = repository;
                Title = repository.Name;
                Tooltip = repository.GitHttpsUrl;
                IsRepository = true;
            }
        }

        // ===== 私有字段（对照 WPF）=====
        private Account _account;
        private GitServiceRepository[] _repositories;
        private Action<GitServiceRepository, Account> _onClone;

        // ===== 构造函数（对照 WPF）=====
        public AccountRepositoriesTabItem()
        {
            InitializeComponent();
            ShowFallback(Translate("Loading repositories..."), null);
        }

        // ===== Refresh（对照 WPF: public void Refresh(Account account)）=====
        // 对照 WPF:
        //   _account = account;
        //   _icon = account.ServiceType.Icon();
        //   FallbackUserControl.FallbackMessage = Translate("Loading repositories...");
        //   FallbackUserControl.Show();
        //   _jobQueue.Add(... GetRepositories().LoadAll() ...);
        // spike 版:
        //   - ServiceType.Icon() → spike 不需要（XAML 中已用 emoji 占位）
        //   - JobQueue + GetRepositories() → 调用方通过 SetRepositories 后续注入
        public void Refresh(Account account)
        {
            _account = account;
            ShowFallback(Translate("Loading repositories..."), null);
        }

        // ===== SetRepositories（spike 新增，替代 WPF JobQueue + GetRepositories 异步流程）=====
        // 调用方完成 Git 服务请求后通过此方法注入仓库列表
        public void SetRepositories(GitServiceRepository[] repositories, Action<GitServiceRepository, Account> onClone = null)
        {
            _repositories = repositories ?? Array.Empty<GitServiceRepository>();
            _onClone = onClone;
            UpdateList(FilterTextBox?.Text ?? string.Empty);
        }

        // ===== SetError（spike 新增，替代 WPF FallbackUserControl 错误显示）=====
        public void SetError(string title, string message)
        {
            ShowFallback(message, title);
            RepositoriesListBox.ItemsSource = null;
        }

        // ===== FilterTextBox_TextChanged（对照 WPF FilterPanel_FilterRequestChanged + UpdateList）=====
        // 对照 WPF: private void FilterPanel_FilterRequestChanged(object sender, EventArgs e)
        //           private void UpdateList(string filterString)
        //   WPF: List<GitServiceRepository> repositories = _repositories.Filter(x => x.Name.ToLower().Contains(filter));
        //         RepositoriesListBox.ItemsSource = GetAccountItems(repositories, _icon);
        // spike 版: 同样逻辑
        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateList(FilterTextBox?.Text ?? string.Empty);
        }

        // ===== CloneButton_Click（对照 WPF）=====
        // 对照 WPF: private void CloneButton_Click(object sender, RoutedEventArgs e)
        //   WPF: if (sender is Button { DataContext: AccountRepositoryItem dataContext })
        //          MainWindow.Commands.ShowCloneWindow.Execute(dataContext.Repository.GitHttpsUrl, _account);
        // spike 版: 调用注入的 _onClone 回调
        private void CloneButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is AccountRepositoryItem dataContext)
            {
                _onClone?.Invoke(dataContext.Repository, _account);
            }
        }

        // ===== UpdateList（对照 WPF）=====
        // 对照 WPF: private void UpdateList(string filterString)
        // spike 版: 同样逻辑
        private void UpdateList(string filterString)
        {
            if (_repositories == null || _repositories.Length == 0)
            {
                // 还在加载中，不更新列表
                return;
            }

            string lower = (filterString ?? string.Empty).ToLowerInvariant();
            IEnumerable<GitServiceRepository> repositories = _repositories
                .Where(x => x.Name != null && x.Name.ToLowerInvariant().Contains(lower));

            AccountItem[] items = GetAccountItems(repositories.ToList());
            RepositoriesListBox.ItemsSource = items;
            HideFallback();
        }

        // ===== GetAccountItems（对照 WPF）=====
        // 对照 WPF: private AccountItem[] GetAccountItems(IReadOnlyList<GitServiceRepository> repositories, ImageSource icon)
        //   WPF: 按 Owner 分组 → 每组先 AccountHeaderItem(owner) 后 AccountRepositoryItem(repo)
        // spike 版: 同样逻辑
        private static AccountItem[] GetAccountItems(IReadOnlyList<GitServiceRepository> repositories)
        {
            Dictionary<string, List<GitServiceRepository>> grouped = repositories
                .GroupBy(x => x.Owner ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.ToList());

            List<AccountItem> list = new List<AccountItem>(24);
            foreach (KeyValuePair<string, List<GitServiceRepository>> item in grouped)
            {
                list.Add(new AccountHeaderItem(item.Key));
                foreach (GitServiceRepository repo in item.Value)
                {
                    list.Add(new AccountRepositoryItem(repo));
                }
            }
            return list.ToArray();
        }

        // ===== 辅助方法（对照 WPF FallbackUserControl.Show/Collapse）=====
        private void ShowFallback(string message, string title)
        {
            if (FallbackPanel != null) FallbackPanel.IsVisible = true;
            if (FallbackMessage != null) FallbackMessage.Text = message ?? string.Empty;
            if (FallbackTitle != null) FallbackTitle.Text = title ?? string.Empty;
            if (FallbackTitle != null) FallbackTitle.IsVisible = !string.IsNullOrEmpty(title);
        }

        private void HideFallback()
        {
            if (FallbackPanel != null) FallbackPanel.IsVisible = false;
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
