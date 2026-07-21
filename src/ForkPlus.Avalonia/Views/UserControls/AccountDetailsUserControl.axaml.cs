using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Accounts;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 AccountDetailsUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/AccountDetailsUserControl.xaml.cs（64 行）：
    //   - ShowDetails(Account)：保存 _account + Refresh()
    //   - Hyperlink_RequestNavigate：e.Uri.OpenInBrowser()
    //   - TabControl_SelectionChanged：刷新当前选中 Tab
    //   - Refresh()：if (_account == null) FallbackUserControl.Show(); return;
    //                FallbackUserControl.Hide();
    //                AvatarImage.Url = _account.AvatarUrl;
    //                HeaderUserNameTextBlock.Text = _account.Username;
    //                HeaderProfileUrlHyperlink.NavigateUri = new Uri(_account.ServerUrl);
    //                HeaderProfileUrlTextBlock.Text = _account.ServerUrl;
    //                if (selectedItem is AccountTabItem) AccountTabItem.Refresh(_account);
    //                else if (selectedItem is AccountRepositoriesTabItem) AccountRepositoriesTabItem.Refresh(_account);
    //
    // Avalonia 版差异（spike 简化）：
    //   - WPF controls:AvatarImage.Url → spike 不真实加载（emoji 占位）
    //   - WPF Hyperlink RequestNavigate → Button Click 事件
    //   - WPF e.Uri.OpenInBrowser() → onOpenProfileUrl 回调注入（Avalonia 工程暂未迁移 OpenInBrowser）
    //   - WPF ModernTabControl.SelectedItem → TabControl.SelectedItem
    //   - WPF FallbackUserControl.Show/Hide → Border.IsVisible = true/false
    public partial class AccountDetailsUserControl : UserControl
    {
        // ===== 私有字段（对照 WPF）=====
        private Account _account;
        private Action<string> _onOpenProfileUrl;

        // ===== 构造函数（对照 WPF）=====
        public AccountDetailsUserControl()
        {
            InitializeComponent();
        }

        // ===== Initialize（spike 新增，注入 onOpenProfileUrl 回调）=====
        // spike 简化：WPF e.Uri.OpenInBrowser() → 回调注入
        public void Initialize(Action<string> onOpenProfileUrl = null)
        {
            _onOpenProfileUrl = onOpenProfileUrl;
        }

        // ===== ShowDetails（对照 WPF: public void ShowDetails(Account account)）=====
        public void ShowDetails(Account account)
        {
            _account = account;
            Refresh();
        }

        // ===== HeaderProfileUrlButton_Click（对照 WPF Hyperlink_RequestNavigate）=====
        // 对照 WPF: private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        //           { e.Uri.OpenInBrowser(); }
        // spike 版：触发注入的 onOpenProfileUrl 回调
        private void HeaderProfileUrlButton_Click(object sender, RoutedEventArgs e)
        {
            if (_account?.ServerUrl != null)
            {
                _onOpenProfileUrl?.Invoke(_account.ServerUrl);
            }
        }

        // ===== TabControl_SelectionChanged（对照 WPF）=====
        // 对照 WPF: private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //   WPF: if (e.OriginalSource is TabControl) Refresh();
        // spike 版: 同样逻辑（仅当事件源是 TabControl 时刷新）
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source == AccountDetailsTabControl)
            {
                Refresh();
            }
        }

        // ===== Refresh（对照 WPF）=====
        // 对照 WPF: private void Refresh()
        //   WPF: if (_account == null) { FallbackUserControl.Show(); return; }
        //         FallbackUserControl.Hide();
        //         AvatarImage.Url = _account.AvatarUrl;
        //         HeaderUserNameTextBlock.Text = _account.Username;
        //         HeaderProfileUrlHyperlink.NavigateUri = new Uri(_account.ServerUrl);
        //         HeaderProfileUrlTextBlock.Text = _account.ServerUrl;
        //         if (selected is AccountTabItem) AccountTabItem.Refresh(_account);
        //         else if (selected is AccountRepositoriesTabItem) AccountRepositoriesTabItem.Refresh(_account);
        // spike 版:
        //   - AvatarImage.Url → 不真实加载（emoji 占位保留）
        //   - TabControl.SelectedItem → 按 TabItem Name 判断
        public void Refresh()
        {
            if (_account == null)
            {
                ShowFallback();
                return;
            }
            HideFallback();

            // AvatarImage.Url spike 不加载（emoji 占位）
            HeaderUserNameTextBlock.Text = _account.Username;
            HeaderLoginTextBlock.Text = _account.Username;
            HeaderProfileUrlTextBlock.Text = _account.ServerUrl;

            // 按当前选中 Tab 刷新对应子控件（对照 WPF TabControl.SelectedItem 判断）
            if (AccountDetailsTabControl?.SelectedItem is TabItem selectedTab)
            {
                if (selectedTab.Name == "AccountTabItem" && AccountTabItemContent != null)
                {
                    AccountTabItemContent.Refresh(_account, supportsNotifications: false);
                }
                else if (selectedTab.Name == "AccountRepositoriesTabItem" && AccountRepositoriesTabItemContent != null)
                {
                    AccountRepositoriesTabItemContent.Refresh(_account);
                }
            }
        }

        // ===== 辅助方法（对照 WPF FallbackUserControl.Show/Hide）=====
        private void ShowFallback()
        {
            if (FallbackPanel != null) FallbackPanel.IsVisible = true;
        }

        private void HideFallback()
        {
            if (FallbackPanel != null) FallbackPanel.IsVisible = false;
        }
    }
}
