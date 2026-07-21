using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.x：Avalonia 版 WelcomeWindow（真实迁移版，对照 WPF WelcomeWindow.xaml.cs 137 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/WelcomeWindow.xaml.cs：
    //   - public partial class WelcomeWindow : ForkPlusDialogWindow
    //   - IsSubmitAllowed: DefaultCloneDirectoryTextBox.Text 非空 + 非 c:\ + Directory.Exists
    //   - 构造函数：
    //     * ShowLogo=false
    //     * TitleTextBlock.FontSize=18
    //     * TitleTextBlock.Foreground=TryFindResource("ForegroundBrush.WindowsInfo") as Brush
    //     * DialogTitle/Description/SubmitButtonTitle 翻译
    //     * ProgressBarContainer.Collapse()
    //     * Refresh() → new GetGlobalUserIdentityGitCommand().Execute().Result →
    //       UserNameTextBox/EmailNameTextBox
    //     * DefaultCloneDirectoryTextBox.Text = Environment.ExpandEnvironmentVariables("%userprofile%")
    //   - OnSubmit: username/email/text + RepositoryManager.Instance.SetSourceDirs(new[]{text}) +
    //     DisableEditableControls + Task.Run(SetGlobalUserIdentityGitCommand +
    //     RescanUserRepositoriesCommand(reset:true)) + 设置 ForkPlusSettings.Default.Guid +
    //     EnableEditableControls + CloseWithOk()；失败时 new ErrorWindow(null, error).ShowDialog()
    //     + Application.Current.Shutdown()
    //   - 3 个 TextBox TextChanged → UpdateSubmitButton()
    //   - BrowseButton_Click: OpenDialog.SelectDirectory 选目录
    //   - Translate: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. spike 构造函数注入 (Action<string>? onSetSourceDir, Action? onRescanRepositories,
    //      Action? onShutdown) 解耦 RepositoryManager / Application.Current.Shutdown
    //   3. spike 版保留 GetGlobalUserIdentityGitCommand 调用（在 ForkPlus.Git.Commands 命名空间，
    //      Core 工程已通过 InternalsVisibleTo 暴露给 ForkPlus.Avalonia）
    //   4. Environment.ExpandEnvironmentVariables("%userprofile%") 跨平台替换：
    //      Windows 用 %userprofile%，Unix 用 Environment.GetFolderPath(SpecialFolder.UserProfile)
    //   5. spike 版 SetGlobalUserIdentityGitCommand 通过 Task.Run 调用，成功后调用注入的
    //      onSetSourceDir?.Invoke(text) 和 onRescanRepositories?.Invoke() 回调
    //   6. 失败时显示 ErrorWindow 替换为：在 DialogDescription 中显示错误 + 不调用 CloseWithOk；
    //      onShutdown 在错误时调用
    //   7. TitleTextBlock.Foreground 用 Application.Current!.TryGetResource(
    //      "ForegroundBrush.WindowsInfo", null, out var brush) as IBrush
    //   8. Visibility.Collapsed → IsVisible=false（spike 版 ProgressBarContainer 省略，
    //      直接用 ForkPlusDialogFooter 的 BusyIndicator 替代）
    //   9. OpenDialog.SelectDirectory → StorageProvider.OpenFolderPickerAsync
    //  10. PreferencesLocalization → ServiceLocator.Localization.Translate
    //  11. spike 基类不提供 DisableEditableControls/EnableEditableControls → 手动禁用 3 个 TextBox + BrowseButton
    public partial class WelcomeWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly Action<string>? _onSetSourceDir;
        private readonly Action? _onRescanRepositories;
        private readonly Action? _onShutdown;

        // 构造函数签名与 WPF 不同：注入 Action 回调替代 RepositoryManager / Application.Current.Shutdown 依赖
        public WelcomeWindow(
            Action<string>? onSetSourceDir = null,
            Action? onRescanRepositories = null,
            Action? onShutdown = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _onSetSourceDir = onSetSourceDir;
            _onRescanRepositories = onRescanRepositories;
            _onShutdown = onShutdown;

            // 对照 WPF: base.ShowLogo = false;
            // spike 版基类不提供 ShowLogo，子类 axaml 中不放 Logo Image 即等价

            // 对照 WPF: base.TitleTextBlock.FontSize = 18.0;
            TitleTextBlock.FontSize = 18.0;

            // 对照 WPF: base.TitleTextBlock.Foreground = Application.Current.TryFindResource("ForegroundBrush.WindowsInfo") as Brush;
            // Avalonia 11: Application.Current!.TryGetResource(key, null, out obj)
            if (Application.Current!.TryGetResource("ForegroundBrush.WindowsInfo", null, out var brushObj)
                && brushObj is IBrush ibrush)
            {
                TitleTextBlock.Foreground = ibrush;
            }

            // 对照 WPF: base.DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("User information");
            DialogDescription = Translate("Set up your user name and email address. This information will be associated with your Git commits.");
            SubmitButtonTitle = Translate("Finish");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("User information");

            // 对照 WPF: ProgressBarContainer.Collapse();
            // spike 版：用 ForkPlusDialogFooter 的 BusyIndicator 替代（SetStatus 控制）
            // 初始保持 None 状态，无可见进度

            // 对照 WPF: Refresh();
            Refresh();

            // 对照 WPF: DefaultCloneDirectoryTextBox.Text = Environment.ExpandEnvironmentVariables("%userprofile%");
            DefaultCloneDirectoryTextBox.Text = GetDefaultUserProfilePath();

            // 对照 WPF: UpdateSubmitButton()（构造结束后基类 SetFooter 已调用，这里再调用确保最新状态）
            UpdateSubmitButton();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                string text = (DefaultCloneDirectoryTextBox.Text ?? "").Trim();
                if (text == "" || text.Equals("c:\\", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                try
                {
                    if (!Directory.Exists(text))
                    {
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to check '" + text + "' existence", ex);
                    return false;
                }
                return true;
            }
        }

        // 对照 WPF: protected override async void OnSubmit()
        protected override async void OnSubmit()
        {
            try
            {
                string username = (UserNameTextBox.Text ?? "").Trim();
                string email = (EmailNameTextBox.Text ?? "").Trim();
                string text = (DefaultCloneDirectoryTextBox.Text ?? "").Trim();

                // 对照 WPF: RepositoryManager.Instance.SetSourceDirs(new string[1] { text });
                // spike 版：onSetSourceDir 在 Task.Run 成功后调用（与 onRescanRepositories 一起）

                DisableEditableControls();
                SetStatus(ForkPlusDialogStatus.InProgress, Translate("Saving user information..."));

                // 对照 WPF: await Task.Run(delegate { SetGlobalUserIdentityGitCommand + RescanUserRepositoriesCommand });
                GitCommandResult gitCommandResult = await Task.Run(delegate
                {
                    if (username != "" && email != "")
                    {
                        GitCommandResult identityResult = new SetGlobalUserIdentityGitCommand().Execute(new UserIdentity(username, email));
                        if (!identityResult.Succeeded)
                        {
                            return identityResult;
                        }
                    }
                    return GitCommandResult.Success();
                });

                if (!gitCommandResult.Succeeded)
                {
                    // 对照 WPF: new ErrorWindow(null, gitCommandResult.Error).ShowDialog();
                    //           Application.Current.Shutdown();
                    // spike 版：在 DialogDescription 中显示错误 + 不调用 CloseWithOk + onShutdown
                    SetStatus(ForkPlusDialogStatus.Error, gitCommandResult.Error?.FriendlyDescription ?? Translate("Failed to set user identity"));
                    DialogDescription = gitCommandResult.Error?.FriendlyDescription ?? Translate("Failed to set user identity");
                    EnableEditableControls();
                    try { _onShutdown?.Invoke(); }
                    catch (Exception ex) { Log.Error("WelcomeWindow onShutdown callback failed", ex); }
                    return;
                }

                // 对照 WPF: new RescanUserRepositoriesCommand().Execute(reset: true);
                // spike 版：onSetSourceDir + onRescanRepositories 回调（成功后调用）
                try { _onSetSourceDir?.Invoke(text); }
                catch (Exception ex) { Log.Error("WelcomeWindow onSetSourceDir callback failed", ex); }
                try { _onRescanRepositories?.Invoke(); }
                catch (Exception ex) { Log.Error("WelcomeWindow onRescanRepositories callback failed", ex); }

                // 对照 WPF: ForkPlusSettings.Default.Guid = Guid.NewGuid().ToString();
                //           ForkPlusSettings.Default.Save();
                ForkPlusSettings.Default.Guid = Guid.NewGuid().ToString();
                ForkPlusSettings.Default.Save();

                SetStatus(ForkPlusDialogStatus.None, string.Empty);
                EnableEditableControls();
                CloseWithOk();
            }
            catch (Exception ex)
            {
                Log.Error("OnSubmit failed", ex);
                SetStatus(ForkPlusDialogStatus.Error, ex.Message);
            }
        }

        // 对照 WPF: UserNameTextBox_TextChanged
        public void UserNameTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
        }

        // 对照 WPF: EmailNameTextBox_TextChanged
        public void EmailNameTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
        }

        // 对照 WPF: DefaultCloneDirectoryTextBox_TextChanged
        public void DefaultCloneDirectoryTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
        }

        // 对照 WPF: BrowseButton_Click
        public async void BrowseButton_Click(object? sender, RoutedEventArgs e)
        {
            // 对照 WPF: string initialDirectory = Environment.ExpandEnvironmentVariables("%userprofile%");
            string initialDirectory = GetDefaultUserProfilePath();
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var options = new FolderPickerOpenOptions
            {
                Title = Translate("Select location"),
            };
            if (Directory.Exists(initialDirectory))
            {
                try
                {
                    var uri = new Uri(Path.GetFullPath(initialDirectory));
                    var folder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(uri);
                    if (folder != null) options.SuggestedStartLocation = folder;
                }
                catch { }
            }

            var result = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            if (result != null && result.Count > 0)
            {
                DefaultCloneDirectoryTextBox.Text = result[0].Path.LocalPath;
                DefaultCloneDirectoryTextBox.Focus();
            }
        }

        // 对照 WPF: private void Refresh()
        private void Refresh()
        {
            // 对照 WPF: UserIdentity result = new GetGlobalUserIdentityGitCommand().Execute().Result;
            //           UserNameTextBox.Text = result.Name ?? "";
            //           EmailNameTextBox.Text = result.Email ?? "";
            // spike 版：保留 GetGlobalUserIdentityGitCommand 调用（Core 工程已通过 InternalsVisibleTo 暴露）
            try
            {
                UserIdentity result = new GetGlobalUserIdentityGitCommand().Execute().Result;
                UserNameTextBox.Text = result.Name ?? "";
                EmailNameTextBox.Text = result.Email ?? "";
            }
            catch (Exception ex)
            {
                Log.Error("WelcomeWindow Refresh failed", ex);
                UserNameTextBox.Text = "";
                EmailNameTextBox.Text = "";
            }
        }

        // spike 版：跨平台获取用户主目录（对照 WPF Environment.ExpandEnvironmentVariables("%userprofile%")）
        // Windows 用 %userprofile%，Unix 用 Environment.GetFolderPath(SpecialFolder.UserProfile)
        private static string GetDefaultUserProfilePath()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                return Environment.ExpandEnvironmentVariables("%userprofile%");
            }
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? "";
        }

        // spike 版：手动禁用可编辑控件（基类不提供 DisableEditableControls）
        private void DisableEditableControls()
        {
            UserNameTextBox.IsEnabled = false;
            EmailNameTextBox.IsEnabled = false;
            DefaultCloneDirectoryTextBox.IsEnabled = false;
            BrowseButton.IsEnabled = false;
        }

        // spike 版：手动启用可编辑控件（基类不提供 EnableEditableControls）
        private void EnableEditableControls()
        {
            UserNameTextBox.IsEnabled = true;
            EmailNameTextBox.IsEnabled = true;
            DefaultCloneDirectoryTextBox.IsEnabled = true;
            BrowseButton.IsEnabled = true;
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
