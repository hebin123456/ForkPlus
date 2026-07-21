using System;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.Git.Commands;
using ForkPlus.Services;
using ForkPlus.Shell.Commands;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.11b：Avalonia 版 SshPassphraseWindow（真实迁移版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/SshPassphraseWindow.xaml.cs（67 行）：
    //   - public partial class SshPassphraseWindow : ForkPlusDialogWindow
    //   - 字段：_sshKeyPath
    //   - IsSubmitAllowed override：!string.IsNullOrWhiteSpace(PasswordBox.Password)
    //   - 构造函数 (sshKeyName, sshKeyPath)：
    //     * DialogTitle = "Passphrase for SSH key"
    //     * DialogDescription = Format("Enter passphrase for SSH key '{0}'", sshKeyName)
    //     * SubmitButtonTitle = "OK"
    //     * PasswordBox.PasswordChanged → UpdateSubmitButton
    //     * PasswordBox.Focus()
    //   - OnSubmit：
    //     * password = PasswordBox.Password
    //     * gitCommandResult = new ValidateSshKeyShellCommand().Execute(_sshKeyPath, password)
    //     * if !Succeeded → new ErrorWindow(null, gitCommandResult.Error).ShowDialog()
    //     * if Result == IncorrectPassphrase → SetStatus(Warning, "Incorrect passphrase") + Focus + SelectAll
    //     * if Result == Success → WindowsCredentialManager.StoreSshPassphrase + CloseWithOk
    //
    // 调用方（WPF 版）：
    //   var window = new SshPassphraseWindow(sshKeyName, sshKeyPath);
    //   if (window.ShowDialog() == true) { /* 密码正确，已存入 Credential Manager */ }
    //
    // 调用方（Avalonia 版）：
    //   var window = new SshPassphraseWindow(sshKeyName, sshKeyPath);
    //   var result = await window.ShowDialog<bool?>(owner);
    //   if (result == true) { /* 密码正确，已存入 Credential Manager */ }
    public partial class SshPassphraseWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly string _sshKeyPath;

        public SshPassphraseWindow(string sshKeyName, string sshKeyPath)
        {
            _sshKeyPath = sshKeyPath ?? "";

            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            string title = Current("Passphrase for SSH key");
            Title = title;
            DialogTitle = title;
            DialogDescription = FormatCurrent("Enter passphrase for SSH key '{0}'", sshKeyName ?? "");
            SubmitButtonTitle = Current("OK");

            // 对照 WPF: PasswordBox.Focus()（Avalonia 需在 Loaded 后调用）
            Dispatcher.UIThread.Post(() =>
            {
                try { PasswordBox.Focus(); }
                catch { /* 控件可能已释放 */ }
            });
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        // 检查 PasswordBox.Text（WPF 是 .Password）非空白
        protected override bool IsSubmitAllowed
        {
            get
            {
                ClearStatus();
                return !string.IsNullOrWhiteSpace(PasswordBox.Text);
            }
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            // Avalonia 11 无 PasswordBox.Password，用 TextBox.Text（PasswordChar 掩码显示）
            string password = PasswordBox.Text;

            // 对照 WPF: new ValidateSshKeyShellCommand().Execute(_sshKeyPath, password)
            GitCommandResult<ValidateSshKeyShellCommand.Result> gitCommandResult;
            try
            {
                gitCommandResult = new ValidateSshKeyShellCommand().Execute(_sshKeyPath, password);
            }
            catch (Exception ex)
            {
                SetStatus(ForkPlusDialogStatus.Error, ex.Message);
                return;
            }

            // 对照 WPF: if (!gitCommandResult.Succeeded) new ErrorWindow(null, gitCommandResult.Error).ShowDialog()
            // spike 版无 ErrorWindow，用 SetStatus(Error, ...) 显示错误（避免阻塞 + 简化依赖）
            if (!gitCommandResult.Succeeded)
            {
                string errText = gitCommandResult.Error?.FriendlyDescription ?? "Failed to validate SSH key";
                SetStatus(ForkPlusDialogStatus.Error, errText);
                return;
            }

            // 对照 WPF: if (Result == IncorrectPassphrase) SetStatus(Warning, ...) + Focus + SelectAll
            if (gitCommandResult.Result == ValidateSshKeyShellCommand.Result.IncorrectPassphrase)
            {
                SetStatus(ForkPlusDialogStatus.Warning, Current("Incorrect passphrase"));
                PasswordBox.Focus();
                PasswordBox.SelectionStart = 0;
                PasswordBox.SelectionEnd = PasswordBox.Text?.Length ?? 0;
                return;
            }

            // 对照 WPF: if (Result == Success) WindowsCredentialManager.StoreSshPassphrase + CloseWithOk
            if (gitCommandResult.Result == ValidateSshKeyShellCommand.Result.Success)
            {
                try
                {
                    WindowsCredentialManager.StoreSshPassphrase(PathHelper.NormalizeUnix(_sshKeyPath), password);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to store SSH passphrase in credential manager: " + ex.Message);
                }
                CloseWithOk();
            }
        }

        // 对照 WPF: PasswordBox.PasswordChanged → UpdateSubmitButton
        // Avalonia 11 用 TextBox.TextChanged 事件
        private void PasswordBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
        }

        // PreferencesLocalization.Current(text) → ServiceLocator.Localization.Current(text)
        private static string Current(string text)
        {
            var localization = ServiceLocator.Localization;
            return localization != null ? localization.Current(text) : text;
        }

        // PreferencesLocalization.FormatCurrent(text, args) → ServiceLocator.Localization.FormatCurrent(text, args)
        private static string FormatCurrent(string text, params object[] args)
        {
            var localization = ServiceLocator.Localization;
            return localization != null ? localization.FormatCurrent(text, args) : string.Format(text, args);
        }
    }
}
