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
    // Phase 4.24b：Avalonia 版 GitHubLoginWindow（真实迁移版，对照 WPF GitHubLoginWindow.xaml.cs 171 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/Accounts/GitHubLoginWindow.xaml.cs：
    //   - public partial class GitHubLoginWindow : ForkPlusDialogWindow, IServiceLoginWindow
    //   - 字段: JobQueue _jobQueue / AuthenticationItem[] / Account Account
    //   - 构造函数 (Account account = null):
    //     * SubmitButtonTitle = "Sign In"
    //     * AuthenticationTypeComboBox.ItemsSource = _authenticationItems
    //     * SelectAuthenticationType(AccessToken)
    //     * TokenTextBox.TextChanged → UpdateSubmitButton
    //   - IsSubmitAllowed override: ComboBox 选中 AccessToken + !IsNullOrEmpty(TokenTextBox.Text)
    //   - OnSubmit: AuthenticateWithAccessToken
    //   - AuthenticateWithAccessToken:
    //     * 构造 GitHubAccessTokenAuthentication / Connection / GitHubService
    //     * service.GetUser() → 成功则 LogOut 旧 + 创建新 Account + AddOrUpdate + CloseWithOk
    //   - OpenPersonalAccessTokenConfigurationUrlButton_Click: 打开 GitHub tokens URL
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. PreferencesLocalization.Current → ServiceLocator.Localization.Translate
    //   3. PlaceholderTextBox.Placeholder → TextBox Watermark
    //   4. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post
    //   5. DisableEditableControls/EnableEditableControls → 手动禁用 TokenTextBox + ComboBox
    //   6. MainWindow.ActiveRepositoryUserControl?.InvalidateAndRefresh → Action? onAccountChanged 回调
    //   7. base.Dispatcher.Async(TokenTextBox.Focus()) → Dispatcher.UIThread.Post(TokenTextBox.Focus())
    //   8. TextChanged 事件签名: EventArgs → TextChangedEventArgs
    //   9. SelectionChangedEventArgs → Avalonia 同名类型（namespace Avalonia.Controls）
    public partial class GitHubLoginWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow, IServiceLoginWindow
    {
        private readonly Action? _onAccountChanged;

        private readonly AuthenticationItem[] _authenticationItems = new AuthenticationItem[1]
        {
            new AuthenticationItem(AuthenticationType.AccessToken, "Personal Access Token")
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
                        if (!string.IsNullOrEmpty(TokenTextBox.Text))
                        {
                            return base.IsSubmitAllowed;
                        }
                        return false;
                    default:
                        return false;
                }
            }
        }

        // 构造函数签名与 WPF 相同 + 新增 Action? onAccountChanged 回调
        public GitHubLoginWindow(Account? account = null, Action? onAccountChanged = null)
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

            // 对照 WPF: base.Dispatcher.Async(() => TokenTextBox.Focus());
            Dispatcher.UIThread.Post(() => TokenTextBox.Focus());
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
            // Avalonia spike 版：默认不抛异常，直接返回
        }

        private void AuthenticateWithAccessToken()
        {
            string token = TokenTextBox.Text ?? "";
            GitHubAccessTokenAuthentication authentication = new GitHubAccessTokenAuthentication("https://github.com", null, token);
            Connection connection = new Connection("https://api.github.com", authentication);
            GitHubService tempService = new GitHubService(connection);

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, "Log in to https://github.com...");

            // 对照 WPF: _jobQueue.Add("Get user", () => { ... }, JobFlags.Hidden);
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
                        GitHubAccessTokenAuthentication gitHubAccessTokenAuthentication =
                            new GitHubAccessTokenAuthentication("https://github.com", result.Username, token);
                        if (!gitHubAccessTokenAuthentication.Save())
                        {
                            EnableEditableControls();
                            SetStatus(ForkPlusDialogStatus.Error, "Cannot save authentication");
                        }
                        else
                        {
                            Account account2 = new Account(
                                RemoteType.Github,
                                gitHubAccessTokenAuthentication.AuthenticationType,
                                "https://github.com",
                                null,
                                result.Username,
                                result.AvatarUrl,
                                enableNotifications: true);
                            AccountManager.Current.AddOrUpdate(account2);
                            Account = account2;

                            // 对照 WPF: MainWindow.ActiveRepositoryUserControl?.InvalidateAndRefresh(SubDomain.Remotes);
                            // Avalonia spike: 调用注入的回调
                            try
                            {
                                _onAccountChanged?.Invoke();
                            }
                            catch (Exception ex)
                            {
                                Log.Error("GitHubLoginWindow onAccountChanged callback failed", ex);
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

        // 对照 WPF: TokenTextBox_TextChanged
        public void TokenTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
        }

        private void RefreshAuthenticationDetails()
        {
            // spike 版：只有 AccessToken 一种类型，TokenContainer 始终可见
            // WPF 原版 TokenContainer.Show() 在 spike 版省略（始终可见）
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

        // 对照 WPF: OpenPersonalAccessTokenConfigurationUrlButton_Click
        public void OpenPersonalAccessTokenConfigurationUrlButton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            new Uri("https://github.com/settings/tokens/new?description=Fork&scopes=repo,user,notifications,workflow").OpenInBrowser();
        }

        // spike 版：手动禁用/启用可编辑控件
        private void DisableEditableControls()
        {
            TokenTextBox.IsEnabled = false;
            AuthenticationTypeComboBox.IsEnabled = false;
        }

        private void EnableEditableControls()
        {
            TokenTextBox.IsEnabled = true;
            AuthenticationTypeComboBox.IsEnabled = true;
        }

        // 对照 WPF: PreferencesLocalization.Current(text)
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
