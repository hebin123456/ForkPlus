using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using ForkPlus.Git.Commands;
using ForkPlus.Services;
using ForkPlus.Shell;
using ForkPlus.Shell.Commands;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.10b：Avalonia 版 GenerateNewSshKeyWindow（真实迁移版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/GenerateNewSshKeyWindow.xaml.cs（122 行）：
    //   - public partial class GenerateNewSshKeyWindow : ForkPlusDialogWindow
    //   - private readonly SshKey[] _existingSshKeys
    //   - public string ResultKey { get; private set; }
    //   - IsSubmitAllowed override：KeyFileName 非空 + 不与 _existingSshKeys 重名 + Email 非空
    //   - 构造函数：
    //     * DialogTitle = "Generate new SSH Key"
    //     * DialogDescription = "Generate new ED25519 key"
    //     * SubmitButtonTitle = "Generate"
    //     * _existingSshKeys = new GetLocalSshKeysCommand().Execute()
    //   - GetCommandPreview() override → "ssh-keygen -t ed25519 -C ... -f ..."
    //   - OnSubmit：DisableEditableControls + SetStatus + await GenerateSshKeyShellCommand +
    //     ResultKey = keyName + Close(gitCommandResult)
    //   - KeyFileNameTextBox_TextChanged / EmailTextBox_TextChanged → UpdateSubmitButton + RefreshCommandPreview
    //
    // 调用方（WPF 版）：
    //   var window = new GenerateNewSshKeyWindow();
    //   if (window.ShowDialog() == true) { use window.ResultKey }
    //
    // 调用方（Avalonia 版）：
    //   var window = new GenerateNewSshKeyWindow();
    //   var result = await window.ShowDialog<bool?>(owner);
    //   if (result == true) { use window.ResultKey }
    public partial class GenerateNewSshKeyWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly SshKey[] _existingSshKeys;

        // 对照 WPF: public string ResultKey { get; private set; }
        public string ResultKey { get; private set; }

        public GenerateNewSshKeyWindow()
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            string title = Current("Generate new SSH Key");
            Title = title;
            DialogTitle = title;
            DialogDescription = Current("Generate new ED25519 key");
            SubmitButtonTitle = Current("Generate");

            // 对照 WPF: _existingSshKeys = new GetLocalSshKeysCommand().Execute()
            try
            {
                _existingSshKeys = new GetLocalSshKeysCommand().Execute() ?? Array.Empty<SshKey>();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to enumerate existing SSH keys: " + ex.Message);
                _existingSshKeys = Array.Empty<SshKey>();
            }

            // 刷新 Submit 按钮启用状态 + 命令预览
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        // 检查 KeyFileName 非空 + 不与 _existingSshKeys 重名 + Email 非空
        protected override bool IsSubmitAllowed
        {
            get
            {
                // 对照 WPF: SetStatus(ForkPlusDialogStatus.None, string.Empty)
                ClearStatus();
                if (string.IsNullOrEmpty(KeyFileNameTextBox.Text))
                {
                    return false;
                }
                try
                {
                    if (_existingSshKeys.Any((SshKey x) =>
                        Path.GetFileNameWithoutExtension(x.FilePath) == KeyFileNameTextBox.Text))
                    {
                        SetStatus(ForkPlusDialogStatus.Warning,
                            FormatCurrent("Ssh key '{0}' already exists", KeyFileNameTextBox.Text));
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    SetStatus(ForkPlusDialogStatus.Error, ex.ToString());
                    return false;
                }
                if (string.IsNullOrEmpty(EmailTextBox.Text))
                {
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
                string keyName = KeyFileNameTextBox.Text;
                string email = EmailTextBox.Text;

                // 对照 WPF: DisableEditableControls()
                // spike 版基类不提供，手动禁用本对话框的可编辑控件
                KeyFileNameTextBox.IsEnabled = false;
                EmailTextBox.IsEnabled = false;

                SetStatus(ForkPlusDialogStatus.InProgress, Current("Generating..."));

                // 对照 WPF: await Task.Run(() => new GenerateSshKeyShellCommand().Execute(email, keyName))
                GitCommandResult gitCommandResult = await Task.Run(
                    () => new GenerateSshKeyShellCommand().Execute(email, keyName));

                SetStatus(ForkPlusDialogStatus.None, string.Empty);

                // 对照 WPF: EnableEditableControls()
                KeyFileNameTextBox.IsEnabled = true;
                EmailTextBox.IsEnabled = true;

                if (gitCommandResult.Succeeded)
                {
                    ResultKey = keyName;
                }
                else
                {
                    // 失败：显示错误信息到 Status，不关闭窗口让用户看到错误
                    SetStatus(ForkPlusDialogStatus.Error,
                        gitCommandResult.Error?.FriendlyDescription ?? "Failed to generate SSH key");
                    UpdateSubmitButton();
                    return;
                }

                // 对照 WPF: Close(gitCommandResult) — spike 基类仅提供 CloseWithOk
                CloseWithOk();
            }
            catch (Exception ex)
            {
                Console.WriteLine("OnSubmit failed: " + ex.Message);
                SetStatus(ForkPlusDialogStatus.Error, ex.ToString());
                KeyFileNameTextBox.IsEnabled = true;
                EmailTextBox.IsEnabled = true;
                UpdateSubmitButton();
            }
        }

        // 对照 WPF: private void KeyFileNameTextBox_TextChanged → UpdateSubmitButton + RefreshCommandPreview
        private void KeyFileNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: private void EmailTextBox_TextChanged → UpdateSubmitButton + RefreshCommandPreview
        private void EmailTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override string GetCommandPreview()
        // spike 版基类不提供 RefreshCommandPreview / GetCommandPreview，子类自己维护
        // CommandPreviewTextBlock.Text（参见 ForkPlusDialogWindow.cs spike 注释）
        private void RefreshCommandPreview()
        {
            string keyName = KeyFileNameTextBox.Text;
            string email = EmailTextBox.Text;
            if (string.IsNullOrEmpty(keyName) || string.IsNullOrEmpty(email))
            {
                CommandPreviewTextBlock.Text = "";
                return;
            }
            string sshDir = SystemEnvironment.LocalSSHDirectory;
            string path = (sshDir != null) ? Path.Combine(sshDir, keyName) : keyName;
            if (path.IndexOf(' ') >= 0)
            {
                path = "\"" + path + "\"";
            }
            CommandPreviewTextBlock.Text = "ssh-keygen -t ed25519 -C \"" + email + "\" -f " + path;
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
