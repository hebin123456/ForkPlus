using System;
using System.IO;
using Avalonia.Controls;
using ForkPlus.Git;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.3b：Avalonia 版 AskPassWindow（真实迁移版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/AskPassWindow.xaml.cs（105 行）：
    //   - public partial class AskPassWindow : ForkPlusDialogWindow
    //   - 字段：AskPassRequest _askPassRequest / string _arguments
    //   - public string Result { get; private set; }
    //   - 构造函数 (arguments, repositoryPath)：
    //     * AskPassRequest.Parse(arguments)
    //     * 按 _arguments 内容分支 5 种情况，设置 InputTextBlock/TextBox/PasswordBox/Remember 显隐
    //     * DialogTitle = Path.GetFileName(repositoryPath) 或 "Credentials Required"
    //     * SubmitButtonTitle = "OK"
    //   - OnSubmit:
    //     * 按 _arguments 分支取 Result = InputTextBox.Text 或 InputPasswordBox.Password
    //     * 按 _askPassRequest 类型 + RememberCheckBox.IsChecked：
    //       - SshPassphrase + Remember → WindowsCredentialManager.StoreSshPassphrase
    //       - StartsWith("Enter passphrase") + 解析 KeyPath → WindowsCredentialManager.StoreSshPassphrase
    //       - SshUserPassword + Remember → WindowsCredentialManager.StoreSshUserPassword
    //     * Close()
    //
    // 调用方（WPF 版）：
    //   var window = new AskPassWindow(arguments, repoPath);
    //   if (window.ShowDialog() == true) { use window.Result }
    //
    // 调用方（Avalonia 版）：
    //   var window = new AskPassWindow(arguments, repoPath);
    //   var result = await window.ShowDialog<bool?>(owner);
    //   if (result == true) { use window.Result }
    public partial class AskPassWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private AskPassRequest _askPassRequest;
        private string _arguments;

        // 用户输入的密码或用户名（OnSubmit 时填充）
        public string Result { get; private set; }

        public AskPassWindow(string arguments, string repositoryPath)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _askPassRequest = AskPassRequest.Parse(arguments);
            _arguments = arguments ?? "";

            // 默认隐藏 RememberCheckBox（仅在 SshPassphrase / SshUserPassword 分支显示）
            RememberCheckBox.IsVisible = false;

            // 对照 WPF: base.DialogTitle = (repositoryPath != "") ? Path.GetFileName(repositoryPath) : "Credentials Required"
            string title = !string.IsNullOrEmpty(repositoryPath)
                ? Path.GetFileName(repositoryPath)
                : Current("Credentials Required");
            Title = title;
            DialogTitle = title;

            // 5 个分支
            if (_arguments.StartsWith("Username for"))
            {
                DialogDescription = _arguments;
                InputTextBlock.Text = Current("User Name:");
                InputTextBox.IsVisible = true;
                InputPasswordBox.IsVisible = false;
                InputTextBox.Focus();
            }
            else if (_askPassRequest is AskPassRequest.SshPassphrase sshPassphrase)
            {
                DialogDescription = FormatCurrent("Passphrase for SSH key '{0}'", sshPassphrase.KeyPath);
                InputTextBlock.Text = Current("Passphrase:");
                InputTextBox.IsVisible = false;
                InputPasswordBox.IsVisible = true;
                InputPasswordBox.Focus();
                RememberCheckBox.IsVisible = true;
            }
            else if (_arguments.StartsWith("Enter passphrase"))
            {
                DialogDescription = _arguments;
                InputTextBlock.Text = Current("Passphrase:");
                InputTextBox.IsVisible = false;
                InputPasswordBox.IsVisible = true;
                InputPasswordBox.Focus();
            }
            else if (_askPassRequest is AskPassRequest.SshUserPassword sshUserPassword)
            {
                DialogDescription = FormatCurrent("Passphrase for '{0}'",
                    sshUserPassword.Username + "@" + sshUserPassword.Url.Host);
                InputTextBlock.Text = Current("Password:");
                InputTextBox.IsVisible = false;
                InputPasswordBox.IsVisible = true;
                InputPasswordBox.Focus();
                RememberCheckBox.IsVisible = true;
            }
            else
            {
                DialogDescription = _arguments;
                InputTextBlock.Text = Current("Password:");
                InputTextBox.IsVisible = false;
                InputPasswordBox.IsVisible = true;
                InputPasswordBox.Focus();
            }

            SubmitButtonTitle = Current("OK");
        }

        protected override void OnSubmit()
        {
            // 对照 WPF: 按 _arguments 分支取 Result
            if (_arguments.StartsWith("Username for"))
            {
                Result = InputTextBox.Text;
            }
            else
            {
                // Avalonia 11 没有 PasswordBox.Password，用 TextBox.Text（PasswordChar 掩码显示）
                Result = InputPasswordBox.Text;
            }

            // 对照 WPF: 按 _askPassRequest 类型 + RememberCheckBox.IsChecked 存储
            if (_askPassRequest is AskPassRequest.SshPassphrase sshPassphrase)
            {
                if (RememberCheckBox.IsChecked.GetValueOrDefault())
                {
                    WindowsCredentialManager.StoreSshPassphrase(sshPassphrase.KeyPath, Result);
                }
            }
            else if (_arguments.StartsWith("Enter passphrase"))
            {
                string keyPath = AskPassParser.ParseSshKey(_arguments);
                if (!string.IsNullOrEmpty(keyPath))
                {
                    WindowsCredentialManager.StoreSshPassphrase(keyPath, Result);
                }
            }
            else if (_askPassRequest is AskPassRequest.SshUserPassword sshUserPassword
                     && RememberCheckBox.IsChecked.GetValueOrDefault())
            {
                WindowsCredentialManager.StoreSshUserPassword(sshUserPassword.Url, sshUserPassword.Username, Result);
            }

            Close();
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
