using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.Accounts;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Services;
using ForkPlus.Settings;
using ForkPlus.Utils.Http;

namespace ForkPlus.Avalonia.Dialogs.Accounts
{
    // Phase 4.22b：Avalonia 版 OpenAiLoginWindow（真实迁移版，对照 WPF OpenAiLoginWindow.xaml.cs 80 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/Accounts/OpenAiLoginWindow.xaml.cs：
    //   - public partial class OpenAiLoginWindow : ForkPlusDialogWindow
    //   - 字段: JobQueue _jobQueue = new JobQueue()
    //   - 构造函数 ():
    //     * ShowLogo=false / ShowHeader=false
    //     * SubmitButtonTitle = PreferencesLocalization.Current("Sign In")
    //     * TokenTextBox.TextChanged += UpdateSubmitButton
    //   - IsSubmitAllowed override: SetStatus(None, ""); return !IsNullOrEmpty(TokenTextBox.Text)
    //   - OnSubmit:
    //     * 构造 PrivateAccessTokenAuthentication / Connection / OpenAiService
    //     * DisableEditableControls + SetStatus(InProgress, "Signing in...")
    //     * _jobQueue.Add("Signing in...", () => {
    //         service.Test() → Dispatcher.Async(() => {
    //           if (!result.Succeeded) EnableEditableControls + SetStatus(Error, friendlyMessage)
    //           else if (!authentication.Save()) SetStatus(Error, "Cannot save authentication")
    //           else { ForkPlusSettings.Default.OpenAiLoggedIn = true; CloseWithOk() }
    //         })
    //       })
    //   - TokenConfigurationUrlButton_Click: new Uri("https://platform.openai.com/api-keys").OpenInBrowser()
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. PreferencesLocalization.Current → ServiceLocator.Localization.Translate
    //   3. PlaceholderTextBox.Placeholder → TextBox Watermark
    //   4. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post
    //   5. DisableEditableControls/EnableEditableControls（spike 基类不提供）
    //      → 手动禁用/启用 TokenTextBox 和 SubmitButton
    //   6. TextChanged 事件签名：WPF 用 delegate（EventArgs），Avalonia 用 TextChangedEventArgs
    public partial class OpenAiLoginWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        public OpenAiLoginWindow()
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            // 对照 WPF: base.SubmitButtonTitle = PreferencesLocalization.Current("Sign In");
            SubmitButtonTitle = Translate("Sign In");
            CancelButtonTitle = Translate("Cancel");

            // 对照 WPF: TokenTextBox.TextChanged += delegate { UpdateSubmitButton(); };
            TokenTextBox.TextChanged += TokenTextBox_TextChanged;
            UpdateSubmitButton();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                // 对照 WPF: SetStatus(ForkPlusDialogStatus.None, "");
                ClearStatus();
                return !string.IsNullOrEmpty(TokenTextBox.Text) && base.IsSubmitAllowed;
            }
        }

        private void TokenTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            string text = TokenTextBox.Text ?? "";
            string serverUrl = "https://api.openai.com";
            PrivateAccessTokenAuthentication authentication = new PrivateAccessTokenAuthentication(serverUrl, "generic", text);
            Connection connection = new Connection(serverUrl, authentication);
            OpenAiService service = new OpenAiService(connection);

            // 对照 WPF: DisableEditableControls();
            DisableEditableControls();
            // 对照 WPF: SetStatus(ForkPlusDialogStatus.InProgress, "Signing in...");
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Signing in..."));

            // 对照 WPF: _jobQueue.Add("Signing in...", () => { service.Test(); Dispatcher.Async(...); })
            // Avalonia: Task.Run + Dispatcher.UIThread.Post
            Task.Run(delegate
            {
                ServiceResult<OpenAiResponse> result = service.Test();
                Dispatcher.UIThread.Post(delegate
                {
                    if (!result.Succeeded)
                    {
                        EnableEditableControls();
                        SetStatus(ForkPlusDialogStatus.Error, result.Error.FriendlyMessage);
                    }
                    else if (!authentication.Save())
                    {
                        EnableEditableControls();
                        SetStatus(ForkPlusDialogStatus.Error, "Cannot save authentication");
                    }
                    else
                    {
                        // 对照 WPF: ForkPlusSettings.Default.OpenAiLoggedIn = true;
                        // ServiceLocator.UserSettings 是 IUserSettings 接口，不含 OpenAiLoggedIn（属持久化字段）
                        // 直接通过 ForkPlusSettings.Default 设置
                        ForkPlusSettings.Default.OpenAiLoggedIn = true;
                        ForkPlusSettings.Default.Save();
                        CloseWithOk();
                    }
                });
            });
        }

        // 对照 WPF: TokenConfigurationUrlButton_Click
        public void TokenConfigurationUrlButton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            new Uri("https://platform.openai.com/api-keys").OpenInBrowser();
        }

        // spike 版：手动禁用/启用可编辑控件（替代 WPF 基类的 DisableEditableControls/EnableEditableControls）
        private void DisableEditableControls()
        {
            TokenTextBox.IsEnabled = false;
        }

        private void EnableEditableControls()
        {
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
