using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Accounts;
using ForkPlus.Git;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs.Accounts
{
    // Phase 4.x：Avalonia 版 AccountsWindow（真实迁移版，对照 WPF AccountsWindow.xaml.cs 102 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/Accounts/AccountsWindow.xaml.cs：
    //   - public partial class AccountsWindow : ForkPlusDialogWindow
    //   - 字段: ObservableCollection<AccountViewModel> _accountViewModels
    //   - 构造函数 ()
    //     * base.ShowLogo=false / base.ShowHeader=false
    //     * InitializeComponent()
    //     * base.ResizeMode = CanResizeWithGrip
    //     * Refresh() + AccountsListBox.SelectedItem = _accountViewModels.FirstOrDefault()
    //     * base.CancelButtonTitle = Translate("Close")
    //     * base.ShowSubmitButton = false
    //     * AccountDetailsUserControl.AccountTabItem.UpdateTokenButtonClicked += ...
    //   - AccountsListBox_SelectionChanged: AccountDetailsUserControl.ShowDetails(account)
    //   - AddAccountButton_Click: new AddAccountWindow { Owner = this }.ShowDialog() → Refresh() + Select last
    //     + MainWindow.ActiveRepositoryUserControl?.InvalidateAndRefresh(SubDomain.Remotes)
    //   - LogOutButton_Click: MessageBoxWindow 确认 → AccountManager.Current.LogOut(account)
    //     + Refresh() + Select first + InvalidateAndRefresh(SubDomain.Remotes)
    //   - AccountDetailsUserControl_UpdateTokenButtonClicked: value.ServiceType.GetLoginWindow(value).ShowDialog()
    //     → 替换 _accountViewModels 中对应项
    //   - Refresh: AccountManager.Current.Accounts → ObservableCollection<AccountViewModel>
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. spike 省略 ShowLogo / ShowHeader（spike 基类不强制 Logo/Header）
    //   3. spike 省略 AccountDetailsUserControl（未迁移），用 TextBlock 简化显示账户详情
    //   4. spike 省略 AccountDetailsUserControl_UpdateTokenButtonClicked（依赖未迁移控件）
    //   5. ResizeMode.CanResizeWithGrip → CanResize=True（Avalonia 11 用 CanResize）
    //   6. ShowSubmitButton=false / CancelButtonTitle=Translate("Close")
    //   7. MainWindow.ActiveRepositoryUserControl?.InvalidateAndRefresh → 注入 Action? onAccountChanged 回调
    //   8. new AddAccountWindow { Owner = this }.ShowDialog() → await AddAccountWindow.ShowDialog<bool?>(this)
    //   9. MessageBoxWindow.ShowDialog() → await MessageBoxWindow.ShowDialog<bool?>(this)
    //  10. Image (Account.ServiceType.Icon()) → TextBlock (IconKey)（spike 版省略 PNG）
    //  11. PreferencesLocalization → ServiceLocator.Localization.Translate
    public partial class AccountsWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // 对照 WPF: private ObservableCollection<AccountViewModel> _accountViewModels;
        private ObservableCollection<AccountViewModel> _accountViewModels;

        // onAccountChanged 回调：登录/登出后通知调用方刷新远端列表（解耦 MainWindow 依赖）
        private readonly Action? _onAccountChanged;

        public AccountsWindow(Action? onAccountChanged = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);

            _onAccountChanged = onAccountChanged;

            // 对照 WPF: base.CancelButtonTitle = Translate("Close");
            CancelButtonTitle = Translate("Close");
            // 对照 WPF: base.ShowSubmitButton = false;
            ShowSubmitButton = false;

            // 对照 WPF: Refresh();
            Refresh();
            // 对照 WPF: AccountsListBox.SelectedItem = _accountViewModels.FirstOrDefault();
            AccountsListBox.SelectedItem = _accountViewModels.FirstOrDefault();
        }

        // 对照 WPF: AccountsListBox_SelectionChanged
        public void AccountsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // 对照 WPF: Account account = (AccountsListBox.SelectedItem as AccountViewModel)?.Account;
            //           AccountDetailsUserControl.ShowDetails(account);
            // Avalonia spike: 用 TextBlock 简化显示账户详情
            Account? account = (AccountsListBox.SelectedItem as AccountViewModel)?.Account;
            ShowAccountDetails(account);
        }

        // 对照 WPF: AddAccountButton_Click
        public async void AddAccountButton_Click(object? sender, RoutedEventArgs e)
        {
            // 对照 WPF: if (new AddAccountWindow { Owner = this }.ShowDialog().GetValueOrDefault()) { Activate(); }
            var addAccountWindow = new AddAccountWindow(_onAccountChanged);
            bool? result = await addAccountWindow.ShowDialog<bool?>(this);

            // 对照 WPF: Refresh(); AccountsListBox.SelectedItem = _accountViewModels.LastOrDefault();
            Refresh();
            AccountsListBox.SelectedItem = _accountViewModels.LastOrDefault();

            // 对照 WPF: MainWindow.ActiveRepositoryUserControl?.InvalidateAndRefresh(SubDomain.Remotes);
            // Avalonia spike: 通过 onAccountChanged 回调由调用方处理
            NotifyAccountChanged();
        }

        // 对照 WPF: LogOutButton_Click
        public async void LogOutButton_Click(object? sender, RoutedEventArgs e)
        {
            // 对照 WPF: Account account = (AccountsListBox.SelectedItem as AccountViewModel)?.Account;
            Account? account = (AccountsListBox.SelectedItem as AccountViewModel)?.Account;
            if (account == null)
            {
                return;
            }

            // 对照 WPF: new MessageBoxWindow(string.Format(Translate("Log out of {0}?"), account.ServerUrl),
            //   Translate("You can always log back in at any time"),
            //   Translate("Log out"), Translate("Cancel"), showCancelButton: true, 500.0)
            //   { Owner = this }.ShowDialog().GetValueOrDefault()
            var confirmWindow = new MessageBoxWindow(
                string.Format(Translate("Log out of {0}?"), account.ServerUrl),
                Translate("You can always log back in at any time"),
                Translate("Log out"),
                Translate("Cancel"),
                showCancelButton: true,
                width: 500.0);
            bool? confirmed = await confirmWindow.ShowDialog<bool?>(this);
            if (!confirmed.GetValueOrDefault())
            {
                return;
            }

            // 对照 WPF: AccountManager.Current.LogOut(account);
            AccountManager.Current.LogOut(account);
            // 对照 WPF: Refresh(); AccountsListBox.SelectedItem = _accountViewModels.FirstOrDefault();
            Refresh();
            AccountsListBox.SelectedItem = _accountViewModels.FirstOrDefault();

            // 对照 WPF: MainWindow.ActiveRepositoryUserControl?.InvalidateAndRefresh(SubDomain.Remotes);
            NotifyAccountChanged();
        }

        // 对照 WPF: Refresh()
        private void Refresh()
        {
            // 对照 WPF: Account[] accounts = AccountManager.Current.Accounts;
            //           _accountViewModels = new ObservableCollection<AccountViewModel>(
            //               accounts.Map((Account x) => new AccountViewModel(x)));
            //           AccountsListBox.ItemsSource = _accountViewModels;
            Account[] accounts = AccountManager.Current.Accounts;
            _accountViewModels = new ObservableCollection<AccountViewModel>(
                accounts.Select((Account x) => new AccountViewModel(x)));
            AccountsListBox.ItemsSource = _accountViewModels;
        }

        // spike 版：简化账户详情显示（替代未迁移的 AccountDetailsUserControl）
        private void ShowAccountDetails(Account? account)
        {
            if (account == null)
            {
                AccountDetailsTextBlock.Text = Translate("No account selected.");
                return;
            }
            string service = account.ServiceType.FriendlyName();
            string text = string.Format(
                "{0}: {1}\n{2}: {3}\n{4}: {5}",
                Translate("Service"), service,
                Translate("User"), account.Username,
                Translate("Server"), account.ServerUrl);
            AccountDetailsTextBlock.Text = text;
        }

        // 对照 WPF: MainWindow.ActiveRepositoryUserControl?.InvalidateAndRefresh(SubDomain.Remotes);
        // Avalonia spike: 通过 onAccountChanged 回调由调用方处理
        private void NotifyAccountChanged()
        {
            try
            {
                _onAccountChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error("AccountsWindow onAccountChanged callback failed", ex);
            }
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
