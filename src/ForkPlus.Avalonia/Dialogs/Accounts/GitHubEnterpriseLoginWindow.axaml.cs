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
    // Phase 4.28b：Avalonia 版 GitHubEnterpriseLoginWindow（真实迁移版，对照 WPF GitHubEnterpriseLoginWindow.xaml.cs 122 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/Accounts/GitHubEnterpriseLoginWindow.xaml.cs：
    //   - public partial class GitHubEnterpriseLoginWindow : ForkPlusDialogWindow, IServiceLoginWindow
    //   - 字段: JobQueue / Account / ServerTextBox + TokenTextBox（双字段，与 GiteaLoginWindow 同构）
    //   - IsSubmitAllowed override: Uri.TryCreate(ServerUrl) + !IsNullOrEmpty(Token)
    //   - OnSubmit: 构造 PrivateAccessTokenAuthentication + Connection + GitHubService(gitHubEnterprise: true)
    //     → service.GetUser() → 成功则 LogOut 旧 + 防重复 + 创建新 Account + AddOrUpdate
    //   - OpenPersonalAccessTokenConfigurationUrlButton_Click: 打开 ServerUrl + "/settings/tokens/new?..."
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. PreferencesLocalization.Current → ServiceLocator.Localization.Translate
    //   3. PlaceholderTextBox.Placeholder → TextBox Watermark
    //   4. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post
    //   5. DisableEditableControls/EnableEditableControls → 手动禁用 ServerTextBox + TokenTextBox
    //   6. MainWindow.ActiveRepositoryUserControl?.InvalidateAndRefresh → Action? onAccountChanged 回调
    //   7. TextChanged 事件签名: EventArgs → TextChangedEventArgs
    //
    // 注：本类结构与 GiteaLoginWindow 几乎完全一致（仅 RemoteType / Service 类不同 + 防重复检查）
    public partial class GitHubEnterpriseLoginWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow, IServiceLoginWindow
    {
        private readonly Action? _onAccountChanged;

        public Account? Account { get; private set; }

        private string ServerUrl => (ServerTextBox.Text ?? "").ToLower().TrimEnd(Consts.Chars.Slash);

        protected override bool IsSubmitAllowed
        {
            get
            {
                ClearStatus();
                if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out var _))
                {
                    return false;
                }
                return !string.IsNullOrEmpty(TokenTextBox.Text) && base.IsSubmitAllowed;
            }
        }

        // 构造函数签名与 WPF 相同 + 新增 Action? onAccountChanged 回调
        public GitHubEnterpriseLoginWindow(Account? account = null, Action? onAccountChanged = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetDescriptionTextBlock(DescriptionTextBlock);

            SubmitButtonTitle = Translate("Sign In");
            CancelButtonTitle = Translate("Cancel");

            _onAccountChanged = onAccountChanged;
            Account = account;
            if (account?.ServerUrl != null)
            {
                ServerTextBox.Text = account.ServerUrl;
            }

            ServerTextBox.TextChanged += Text_Changed;
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
            string serverUrl = ServerUrl;
            string token = TokenTextBox.Text ?? "";
            Uri uri = new Uri(serverUrl);
            PrivateAccessTokenAuthentication authentication = new PrivateAccessTokenAuthentication(null, null, token);
            Connection connection = new Connection(serverUrl, authentication);
            GitHubService tempService = new GitHubService(connection, gitHubEnterprise: true);

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, "Log in to " + serverUrl + "...");

            // 对照 WPF: _jobQueue.Add(PreferencesLocalization.Current("Get user"), () => { ... }, JobFlags.Hidden);
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
                        if (AccountManager.Current.FindAccount(uri.Host, result.Username) != null)
                        {
                            EnableEditableControls();
                            SetStatus(ForkPlusDialogStatus.Warning,
                                "You are already logged in to " + serverUrl + " as " + result.Username);
                        }
                        else
                        {
                            PrivateAccessTokenAuthentication privateAccessTokenAuthentication =
                                new PrivateAccessTokenAuthentication(serverUrl, result.Username, token);
                            if (!privateAccessTokenAuthentication.Save())
                            {
                                EnableEditableControls();
                                SetStatus(ForkPlusDialogStatus.Error, "Cannot save authentication");
                            }
                            else
                            {
                                Account account2 = new Account(
                                    RemoteType.GithubEnterprise,
                                    privateAccessTokenAuthentication.AuthenticationType,
                                    serverUrl,
                                    null,
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
                                    Log.Error("GitHubEnterpriseLoginWindow onAccountChanged callback failed", ex);
                                }
                                CloseWithOk();
                            }
                        }
                    });
                }
            });
        }

        // 对照 WPF: OpenPersonalAccessTokenConfigurationUrlButton_Click
        public void OpenPersonalAccessTokenConfigurationUrlButton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            new Uri(ServerUrl + "/settings/tokens/new?description=Fork&scopes=repo,user,notifications,workflow").OpenInBrowser();
        }

        // spike 版：手动禁用/启用可编辑控件
        private void DisableEditableControls()
        {
            ServerTextBox.IsEnabled = false;
            TokenTextBox.IsEnabled = false;
        }

        private void EnableEditableControls()
        {
            ServerTextBox.IsEnabled = true;
            TokenTextBox.IsEnabled = true;
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
