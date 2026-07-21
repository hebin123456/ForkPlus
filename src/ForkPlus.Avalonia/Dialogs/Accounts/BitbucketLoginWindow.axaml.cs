using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.Accounts;
using ForkPlus.Git;
using ForkPlus.Services;
using ForkPlus.Utils.Http;

namespace ForkPlus.Avalonia.Dialogs.Accounts
{
    // Phase 4.26b：Avalonia 版 BitbucketLoginWindow（真实迁移版，对照 WPF BitbucketLoginWindow.xaml.cs 175 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/Accounts/BitbucketLoginWindow.xaml.cs：
    //   - public partial class BitbucketLoginWindow : ForkPlusDialogWindow, IServiceLoginWindow
    //   - 字段: JobQueue _jobQueue / AuthenticationItem[] / Account Account
    //   - 三字段: AuthenticationTypeComboBox + EmailTextBox + TokenTextBox（API Token 用 Basic Auth）
    //   - IsSubmitAllowed override: ComboBox 选中 AccessToken + !IsNullOrEmpty(Email) + !IsNullOrEmpty(Token)
    //   - AuthenticateWithAccessToken:
    //     * 构造 BasicAuthentication(null, email, token) + Connection + BitbucketService
    //     * service.GetUser() → 成功则创建 BitbucketBasicAuthentication + Account + AddOrUpdate
    //   - OpenApiTokensConfigurationUrlButton_Click: 打开 Atlassian API tokens URL
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. PreferencesLocalization.Translate → ServiceLocator.Localization.Translate
    //   3. PlaceholderTextBox.Placeholder → TextBox Watermark
    //   4. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post
    //   5. DisableEditableControls/EnableEditableControls → 手动禁用 EmailTextBox + TokenTextBox + ComboBox
    //   6. MainWindow.ActiveRepositoryUserControl?.InvalidateAndRefresh → Action? onAccountChanged 回调
    //   7. Button.ToolTip → ToolTip.SetTip(button, ...)
    //   8. TextChanged 事件签名: EventArgs → TextChangedEventArgs
    public partial class BitbucketLoginWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow, IServiceLoginWindow
    {
        private readonly Action? _onAccountChanged;

        private readonly AuthenticationItem[] _authenticationItems = new AuthenticationItem[1]
        {
            new AuthenticationItem(AuthenticationType.AccessToken, "API Token")
        };

        public Account? Account { get; private set; }

        protected override bool IsSubmitAllowed
        {
            get
            {
                ClearStatus();
                if (AuthenticationTypeComboBox.SelectedItem is not AuthenticationItem { Type: var type })
                {
                    return false;
                }
                switch (type)
                {
                    case AuthenticationType.AccessToken:
                        if (!string.IsNullOrEmpty(EmailTextBox.Text))
                        {
                            return !string.IsNullOrEmpty(TokenTextBox.Text) && base.IsSubmitAllowed;
                        }
                        return false;
                    default:
                        return false;
                }
            }
        }

        // 构造函数签名与 WPF 相同 + 新增 Action? onAccountChanged 回调
        public BitbucketLoginWindow(Account? account = null, Action? onAccountChanged = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetDescriptionTextBlock(DescriptionTextBlock);

            SubmitButtonTitle = Translate("Sign In");
            CancelButtonTitle = Translate("Cancel");

            _onAccountChanged = onAccountChanged;
            Account = account;
            AuthenticationTypeComboBox.ItemsSource = _authenticationItems;
            SelectAuthenticationType(AuthenticationType.AccessToken);
            if (account?.Email != null)
            {
                EmailTextBox.Text = account.Email;
            }

            // 对照 WPF: OpenApiTokensConfigurationUrlButton.ToolTip = Translate("Required scopes: ...");
            ToolTip.SetTip(OpenApiTokensConfigurationUrlButton,
                Translate("Required scopes:\n read:user:bitbucket\n read:repository:bitbucket\n read:workspace:bitbucket\n write:repository:bitbucket\n read:pullrequest:bitbucket"));

            EmailTextBox.TextChanged += Text_Changed;
            TokenTextBox.TextChanged += Text_Changed;
            UpdateSubmitButton();
        }

        private void Text_Changed(object? sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            if (AuthenticationTypeComboBox.SelectedItem is not AuthenticationItem authenticationItem)
            {
                return;
            }
            if (authenticationItem.Type == AuthenticationType.AccessToken)
            {
                AuthenticateWithAccessToken();
                return;
            }
            // 对照 WPF: throw new CannotReachHereException();
            // Avalonia spike 版：默认不抛异常
        }

        private void AuthenticateWithAccessToken()
        {
            string email = (EmailTextBox.Text ?? "").ToLower();
            string token = TokenTextBox.Text ?? "";
            BasicAuthentication authentication = new BasicAuthentication(null, email, token);
            Connection connection = new Connection("https://api.bitbucket.org", authentication);
            BitbucketService tempService = new BitbucketService(connection);

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, "Log in to https://bitbucket.org...");

            // 对照 WPF: _jobQueue.Add(Translate("Get user"), () => { ... }, JobFlags.Hidden);
            // Avalonia: Task.Run + Dispatcher.UIThread.Post
            Task.Run(delegate
            {
                ServiceResult<User> userResponse = tempService.GetUser();
                if (!userResponse.Succeeded)
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        EnableEditableControls();
                        SetStatus(ForkPlusDialogStatus.Error, userResponse.Error.FriendlyMessage);
                    });
                }
                else
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        Account? existing = Account;
                        if (existing != null)
                        {
                            AccountManager.Current.LogOut(existing);
                        }
                        User result = userResponse.Result;
                        BitbucketBasicAuthentication bitbucketBasicAuthentication =
                            new BitbucketBasicAuthentication("https://bitbucket.org", email, result.Username, token);
                        if (!bitbucketBasicAuthentication.Save())
                        {
                            EnableEditableControls();
                            SetStatus(ForkPlusDialogStatus.Error, "Cannot save authentication");
                        }
                        else
                        {
                            Account account2 = new Account(
                                RemoteType.Bitbucket,
                                bitbucketBasicAuthentication.AuthenticationType,
                                "https://bitbucket.org",
                                email,
                                result.Username,
                                result.AvatarUrl);
                            AccountManager.Current.AddOrUpdate(account2);
                            Account = account2;

                            // 对照 WPF: MainWindow.ActiveRepositoryUserControl?.InvalidateAndRefresh(SubDomain.Remotes);
                            try
                            {
                                _onAccountChanged?.Invoke();
                            }
                            catch (Exception ex)
                            {
                                Log.Error("BitbucketLoginWindow onAccountChanged callback failed", ex);
                            }
                            CloseWithOk();
                        }
                    });
                }
            });
        }

        // 对照 WPF: AuthenticationTypeComboBox_SelectionChanged
        public void AuthenticationTypeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            RefreshAuthenticationDetails();
            UpdateSubmitButton();
        }

        // 对照 WPF: OpenApiTokensConfigurationUrlButton_Click
        public void OpenApiTokensConfigurationUrlButton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            new Uri("https://id.atlassian.com/manage-profile/security/api-tokens").OpenInBrowser();
        }

        private void RefreshAuthenticationDetails()
        {
            // spike 版：只有 AccessToken 一种类型，TokenContainer 始终可见
        }

        private void SelectAuthenticationType(AuthenticationType authenticationType)
        {
            switch (authenticationType)
            {
                case AuthenticationType.AccessToken:
                    foreach (var item in _authenticationItems)
                    {
                        if (item.Type == AuthenticationType.AccessToken)
                        {
                            AuthenticationTypeComboBox.SelectedItem = item;
                            break;
                        }
                    }
                    break;
            }
        }

        // spike 版：手动禁用/启用可编辑控件
        private void DisableEditableControls()
        {
            EmailTextBox.IsEnabled = false;
            TokenTextBox.IsEnabled = false;
            AuthenticationTypeComboBox.IsEnabled = false;
        }

        private void EnableEditableControls()
        {
            EmailTextBox.IsEnabled = true;
            TokenTextBox.IsEnabled = true;
            AuthenticationTypeComboBox.IsEnabled = true;
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
