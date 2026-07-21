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
    // Phase 4.25b：Avalonia 版 GitLabLoginWindow（真实迁移版，对照 WPF GitLabLoginWindow.xaml.cs 147 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/Accounts/GitLabLoginWindow.xaml.cs：
    //   - public partial class GitLabLoginWindow : ForkPlusDialogWindow, IServiceLoginWindow
    //   - 字段: JobQueue _jobQueue / bool _server / Account Account
    //   - 构造函数 (bool server = false, Account account = null):
    //     * !server: 隐藏 Server 输入框（ServerUrl 固定为 "https://gitlab.com"）
    //     * server: ServerTextBox.Text = account?.ServerUrl ?? "https://gitlab.com"
    //   - IsSubmitAllowed override: _server && Uri.TryCreate(ServerUrl) + !IsNullOrEmpty(Token)
    //   - OnSubmit: 根据 serverUrl 判断 RemoteType.Gitlab / RemoteType.GitlabServer
    //     构造 GitLabPrivateAccessTokenAuthentication / Connection / GitLabService
    //     → service.GetUser() → 成功则 LogOut 旧 + 创建新 Account + AddOrUpdate + CloseWithOk
    //   - OpenPersonalAccessTokenConfigurationUrlButton_Click: 打开 GitLab tokens URL
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. PreferencesLocalization.Translate → ServiceLocator.Localization.Translate
    //   3. PlaceholderTextBox.Placeholder → TextBox Watermark
    //   4. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post
    //   5. DisableEditableControls/EnableEditableControls → 手动禁用 ServerTextBox + TokenTextBox
    //   6. MainWindow.ActiveRepositoryUserControl?.InvalidateAndRefresh → Action? onAccountChanged 回调
    //   7. ToolTip 设置：WPF Button.ToolTip="..." → Avalonia ToolTip.SetTip(button, ...)
    //   8. Collapse/Show 扩展方法 → IsVisible = false / true
    public partial class GitLabLoginWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow, IServiceLoginWindow
    {
        private readonly Action? _onAccountChanged;
        private readonly bool _server;

        public Account? Account { get; private set; }

        protected override bool IsSubmitAllowed
        {
            get
            {
                ClearStatus();
                if (_server)
                {
                    if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out var _))
                    {
                        return false;
                    }
                }
                return !string.IsNullOrEmpty(TokenTextBox.Text) && base.IsSubmitAllowed;
            }
        }

        private string ServerUrl
        {
            get
            {
                if (!_server)
                {
                    return "https://gitlab.com";
                }
                return (ServerTextBox.Text ?? "").ToLower().TrimEnd(Consts.Chars.Slash);
            }
        }

        // 构造函数签名与 WPF 相同 + 新增 Action? onAccountChanged 回调
        public GitLabLoginWindow(bool server = false, Account? account = null, Action? onAccountChanged = null)
        {
            ShowFooter = true;
            _server = server;
            InitializeComponent();
            SetFooter(Footer);
            SetDescriptionTextBlock(DescriptionTextBlock);

            SubmitButtonTitle = Translate("Sign In");
            CancelButtonTitle = Translate("Cancel");

            // 对照 WPF: OpenPersonalAccessTokenConfigurationUrlButton.ToolTip = Translate("Required scopes: ...");
            ToolTip.SetTip(OpenPersonalAccessTokenConfigurationUrlButton,
                Translate("Required scopes: read_user, read_api, read_repository, write_repository"));

            _onAccountChanged = onAccountChanged;
            Account = account;

            // 对照 WPF: !server: ServerTextBlock.Collapse() + ServerTextBox.Collapse()
            //          server: ServerTextBox.Text = account?.ServerUrl ?? "https://gitlab.com"
            if (!server)
            {
                ServerTextBlock.IsVisible = false;
                ServerTextBox.IsVisible = false;
            }
            else
            {
                ServerTextBox.Text = account?.ServerUrl ?? "https://gitlab.com";
                ServerTextBlock.IsVisible = true;
                ServerTextBox.IsVisible = true;
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
            RemoteType remoteType = (serverUrl == "https://gitlab.com") ? RemoteType.Gitlab : RemoteType.GitlabServer;
            GitLabPrivateAccessTokenAuthentication authentication = new GitLabPrivateAccessTokenAuthentication(null, null, token);
            Connection connection = new Connection(serverUrl, authentication);
            GitLabService tempService = new GitLabService(connection, remoteType == RemoteType.GitlabServer);

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, "Log in to " + serverUrl + "...");

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
                        GitLabPrivateAccessTokenAuthentication gitLabPrivateAccessTokenAuthentication =
                            new GitLabPrivateAccessTokenAuthentication(serverUrl, result.Username, token);
                        if (!gitLabPrivateAccessTokenAuthentication.Save())
                        {
                            EnableEditableControls();
                            SetStatus(ForkPlusDialogStatus.Error, "Cannot save authentication");
                        }
                        else
                        {
                            Account account2 = new Account(
                                remoteType,
                                gitLabPrivateAccessTokenAuthentication.AuthenticationType,
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
                                Log.Error("GitLabLoginWindow onAccountChanged callback failed", ex);
                            }
                            CloseWithOk();
                        }
                    });
                }
            });
        }

        // 对照 WPF: OpenPersonalAccessTokenConfigurationUrlButton_Click
        public void OpenPersonalAccessTokenConfigurationUrlButton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            new Uri(ServerUrl + "/-/user_settings/personal_access_tokens?name=Fork&scopes=api%2Cwrite_repository").OpenInBrowser();
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
