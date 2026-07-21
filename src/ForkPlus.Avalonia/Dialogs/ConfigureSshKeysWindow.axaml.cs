using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ForkPlus.Git.Commands;
using ForkPlus.Services;
using ForkPlus.Settings;
using ForkPlus.Shell;
using ForkPlus.Shell.Commands;

namespace ForkPlus.Avalonia.Dialogs
{
    // Avalonia 版 ConfigureSshKeysWindow（对照 WPF ConfigureSshKeysWindow.xaml 127 行 + .cs 260 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/ConfigureSshKeysWindow.xaml.cs：
    //   - public partial class ConfigureSshKeysWindow : ForkPlusDialogWindow
    //   - 构造函数：ShowLogo=false / Translate DialogTitle/DialogDescription/SubmitButtonTitle / Refresh / SshKeyListBox.SelectedIndex=0
    //   - OnSubmit：把激活的 SSH key 路径写入 ForkPlusSettings.Default.SshKeys + Save
    //   - SshKeyListBox_SelectionChanged → RefreshDetails
    //   - SshKeyCheckBox_Changed → ValidateSshKey（IncorrectPassphrase 时弹 SshPassphraseWindow）
    //   - GenerateNewSSHKeyMenuItem_Click → new GenerateNewSshKeyWindow().ShowDialog()
    //   - BrowseKeyMenuItem_Click → OpenDialog.SelectFile(.pub) → 加到 SshKeys + Refresh
    //   - CopyPublicKey_RequestNavigate → ServiceLocator.Clipboard.SetText
    //   - Refresh / RefreshConfigutationTextBlock / RefreshStatus / RefreshDetails / GetCustomSshKey
    //   - ValidateSshKey → new ValidateSshKeyShellCommand().Execute(keyPath)
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. DropDownButton + ContextMenu → 两个独立 Button（Avalonia 工程暂无 DropDownButton）
    //   3. Image Source={DynamicResource KeyIcon} → spike 用 emoji TextBlock "🔑"
    //   4. FallbackUserControl → TextBlock + IsVisible 切换
    //   5. Hyperlink RequestNavigate → Button + Click 调 ServiceLocator.Clipboard.SetText
    //   6. SelectableTextBlock → TextBlock + TextTrimming（spike 不支持选择）
    //   7. SshKeyViewModel → 嵌套类（spike 不引入 WPF 工程 SshKeyViewModel.cs）
    //   8. ErrorWindow → spike 用 SetStatus(Error, ...) 显示错误（避免阻塞 + 简化依赖）
    //   9. OpenDialog.SelectFile → TopLevel.StorageProvider.OpenFilePickerAsync
    //  10. ValidateSshKeyShellCommand.Execute → 直接调用（Core 已迁）
    //  11. SshPassphraseWindow → 已迁移，使用 await ShowDialog<bool?>(this)
    public partial class ConfigureSshKeysWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private List<SshKeyViewModel> _sshKeys;

        public ConfigureSshKeysWindow()
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            // 对照 WPF: base.DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Configure SSH Keys");
            DialogDescription = Translate("1. Select or generate a new SSH key which will identify your computer\n2. Copy the public key content to the account section on the website of your git provider");
            SubmitButtonTitle = Translate("OK");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Configure SSH Keys");

            // 对照 WPF: Refresh()
            Refresh();
            // 对照 WPF: SshKeyListBox.SelectedIndex = 0;
            if (SshKeyListBox.ItemCount > 0)
            {
                SshKeyListBox.SelectedIndex = 0;
            }
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            if (_sshKeys == null)
            {
                base.OnSubmit();
                return;
            }
            // 对照 WPF: SshKeyListBox.Items.CompactMap(...).Filter(IsActive).Map(KeyPath)
            string[] sshKeys = _sshKeys
                .Where((SshKeyViewModel x) => x.IsActive)
                .Select((SshKeyViewModel x) => x.KeyPath)
                .ToArray();
            ForkPlusSettings.Default.SshKeys = sshKeys;
            ForkPlusSettings.Default.Save();
            base.OnSubmit();
        }

        // 对照 WPF: SshKeyListBox_SelectionChanged
        private void SshKeyListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshDetails();
        }

        // 对照 WPF: SshKeyCheckBox_Changed（WPF 用 Checked/Unchecked 两个事件，Avalonia 用 IsCheckedChanged）
        private void SshKeyCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is SshKeyViewModel sshKeyViewModel)
            {
                if (sshKeyViewModel.IsActive)
                {
                    sshKeyViewModel.IsActive = ValidateSshKey(sshKeyViewModel.KeyFileName, sshKeyViewModel.KeyPath);
                }
                RefreshConfigutationTextBlock();
                RefreshStatus();
            }
        }

        // 对照 WPF: GenerateNewSSHKeyMenuItem_Click → new GenerateNewSshKeyWindow().ShowDialog()
        private async void GenerateNewSSHKeyButton_Click(object sender, RoutedEventArgs e)
        {
            var generateNewSshKeyWindow = new GenerateNewSshKeyWindow();
            bool? dialogResult = await generateNewSshKeyWindow.ShowDialog<bool?>(this);
            if (dialogResult != true)
            {
                return;
            }
            // 对照 WPF: if (!generateNewSshKeyWindow.GitResult.Succeeded) new ErrorWindow(...).ShowDialog()
            // spike 版：用 SetStatus(Error, ...) 显示错误
            // （GenerateNewSshKeyWindow 在 OnSubmit 失败时不 Close，dialogResult 不会是 true）
            string resultKey = generateNewSshKeyWindow.ResultKey;
            if (resultKey != null)
            {
                Refresh();
                ActivateAndSelectSshKey(resultKey);
            }
        }

        // 对照 WPF: BrowseKeyMenuItem_Click → OpenDialog.SelectFile(.pub)
        private async void BrowseKeyButton_Click(object sender, RoutedEventArgs e)
        {
            string initialDirectory = Environment.ExpandEnvironmentVariables("%userprofile%");
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var options = new FilePickerOpenOptions
            {
                Title = Translate("Select SSH key"),
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("SSH key")
                    {
                        Patterns = new List<string> { "*.pub" }
                    },
                    new FilePickerFileType("All files")
                    {
                        Patterns = new List<string> { "*.*" }
                    }
                }
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

            var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (result == null || result.Count == 0) return;
            string filePath = result[0].Path.LocalPath;

            // 对照 WPF: list.Add(Path.ChangeExtension(filePath, null))
            string[] sshKeys = ForkPlusSettings.Default.SshKeys ?? Array.Empty<string>();
            var list = new List<string>(sshKeys.Length + 1);
            list.AddRange(sshKeys);
            list.Add(Path.ChangeExtension(filePath, null));
            ForkPlusSettings.Default.SshKeys = list.ToArray();
            Refresh();
            SelectAndFocusSshKey(Path.GetFileNameWithoutExtension(filePath));
        }

        // 对照 WPF: CopyPublicKey_RequestNavigate → ServiceLocator.Clipboard.SetText
        private void CopyPublicKey_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ServiceLocator.Clipboard?.SetText(SshKeyPublicKeyTextBox.Text ?? "");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to copy public key: " + ex.Message);
            }
        }

        // 对照 WPF: ActivateAndSelectSshKey
        private void ActivateAndSelectSshKey(string keyName)
        {
            SshKeyViewModel sshKeyViewModel = _sshKeys?.FirstOrDefault((SshKeyViewModel x) => x.KeyFileName == keyName);
            if (sshKeyViewModel != null)
            {
                sshKeyViewModel.IsActive = true;
                SshKeyListBox.SelectedItem = sshKeyViewModel;
                SshKeyListBox.Focus();
            }
        }

        // 对照 WPF: SelectAndFocusSshKey
        private void SelectAndFocusSshKey(string keyName)
        {
            SshKeyViewModel sshKeyViewModel = _sshKeys?.FirstOrDefault((SshKeyViewModel x) => x.KeyFileName == keyName);
            if (sshKeyViewModel != null)
            {
                SshKeyListBox.SelectedItem = sshKeyViewModel;
                SshKeyListBox.Focus();
            }
        }

        // 对照 WPF: ValidateSshKey → new ValidateSshKeyShellCommand().Execute(keyPath)
        private bool ValidateSshKey(string keyName, string keyPath)
        {
            try
            {
                GitCommandResult<ValidateSshKeyShellCommand.Result> gitCommandResult =
                    new ValidateSshKeyShellCommand().Execute(keyPath);
                if (!gitCommandResult.Succeeded)
                {
                    // spike 版：用 SetStatus(Error, ...) 显示错误（避免阻塞）
                    SetStatus(ForkPlusDialogStatus.Error,
                        gitCommandResult.Error?.FriendlyDescription ?? "Failed to validate SSH key");
                    return false;
                }
                if (gitCommandResult.Result == ValidateSshKeyShellCommand.Result.Success)
                {
                    return true;
                }
                if (gitCommandResult.Result == ValidateSshKeyShellCommand.Result.IncorrectPassphrase)
                {
                    // 对照 WPF: new SshPassphraseWindow(keyName, keyPath) { Owner = this }.ShowDialog()
                    // Avalonia: await ShowDialog<bool?>(this)
                    var passphraseWindow = new SshPassphraseWindow(keyName, keyPath);
                    bool? result = passphraseWindow.ShowDialog<bool?>(this).GetAwaiter().GetResult();
                    return result.GetValueOrDefault();
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ValidateSshKey failed: " + ex.Message);
                SetStatus(ForkPlusDialogStatus.Error, ex.Message);
                return false;
            }
        }

        // 对照 WPF: Refresh
        private void Refresh()
        {
            SshKey[] localKeys;
            try
            {
                localKeys = new GetLocalSshKeysCommand().Execute() ?? Array.Empty<SshKey>();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to enumerate SSH keys: " + ex.Message);
                localKeys = Array.Empty<SshKey>();
            }

            var list = new List<SshKeyViewModel>(localKeys.Length);
            foreach (SshKey key in localKeys)
            {
                list.Add(new SshKeyViewModel(key));
            }

            string[] sshKeys = ForkPlusSettings.Default.SshKeys ?? Array.Empty<string>();
            foreach (string activeKeyPath in sshKeys)
            {
                SshKeyViewModel existing = list.FirstOrDefault((SshKeyViewModel x) => x.KeyPath == activeKeyPath);
                if (existing != null)
                {
                    existing.IsActive = true;
                    continue;
                }
                SshKey customSshKey = GetCustomSshKey(activeKeyPath);
                if (customSshKey != null)
                {
                    list.Add(new SshKeyViewModel(customSshKey, isActive: true));
                }
            }

            list.Sort((SshKeyViewModel x, SshKeyViewModel y) =>
                string.Compare(x.KeyFileName, y.KeyFileName, StringComparison.Ordinal));

            _sshKeys = list;
            SshKeyListBox.ItemsSource = list;

            // 对照 WPF: FallbackUserControl.Show()/Collapse()
            bool hasKeys = list.Count > 0;
            FallbackTextBlock.IsVisible = !hasKeys;
            // 对照 WPF: DetailsFallbackUserControl.Show()/Collapse()（spike 简化为不显示独立 fallback）

            RefreshConfigutationTextBlock();
            RefreshStatus();
        }

        // 对照 WPF: RefreshConfigutationTextBlock
        private void RefreshConfigutationTextBlock()
        {
            if (_sshKeys == null) return;
            string[] activeKeys = _sshKeys
                .Where((SshKeyViewModel x) => x.IsActive)
                .Select((SshKeyViewModel x) => x.KeyFileName)
                .ToArray();
            var sb = new StringBuilder(activeKeys.Length);
            foreach (string value in activeKeys)
            {
                if (sb.Length != 0)
                {
                    sb.Append(", ");
                }
                sb.Append(value);
            }
            if (sb.Length == 0)
            {
                sb.Append(Translate("default system ssh-agent"));
                SshConfigurationTextBlock.FontStyle = FontStyle.Italic;
                SshConfigurationIcon.IsVisible = false;
            }
            else
            {
                SshConfigurationTextBlock.FontStyle = FontStyle.Normal;
                SshConfigurationIcon.IsVisible = true;
            }
            SshConfigurationTextBlock.Text = sb.ToString();
        }

        // 对照 WPF: RefreshStatus
        private void RefreshStatus()
        {
            if (_sshKeys == null) return;
            int activeCount = _sshKeys.Count((SshKeyViewModel x) => x.IsActive);
            if (activeCount > 1)
            {
                SetStatus(ForkPlusDialogStatus.Warning,
                    Translate("Note: you can't use multiple SSH keys with the same server"));
            }
            else
            {
                SetStatus(ForkPlusDialogStatus.None, "");
            }
        }

        // 对照 WPF: RefreshDetails
        private void RefreshDetails()
        {
            SshKeyViewModel sshKeyViewModel = SshKeyListBox.SelectedItem as SshKeyViewModel;
            SshKeyPathTextBlock.Text = sshKeyViewModel?.KeyPath ?? "";
            ToolTip.SetTip(SshKeyPathTextBlock, sshKeyViewModel?.KeyPath ?? "");
            SshKeySha256TextBox.Text = sshKeyViewModel?.Sha256 ?? "";
            SshKeyPublicKeyTextBox.Text = sshKeyViewModel?.PublicKey ?? "";
        }

        // 对照 WPF: GetCustomSshKey
        private static SshKey GetCustomSshKey(string privateKeyFilePath)
        {
            if (!File.Exists(privateKeyFilePath))
            {
                // spike 版：返回 null 不弹 ErrorWindow
                Console.WriteLine(string.Format("Cannot find private key: '{0}'", privateKeyFilePath));
                return null;
            }
            string pubPath = Path.ChangeExtension(privateKeyFilePath, ".pub");
            if (!File.Exists(pubPath))
            {
                Console.WriteLine(string.Format("Cannot find public key: '{0}'", pubPath));
                return null;
            }
            try
            {
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(pubPath);
                string rawPublicKey = File.ReadAllText(pubPath);
                return new SshKey(privateKeyFilePath, fileNameWithoutExtension, rawPublicKey);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to read '" + pubPath + "'", ex);
                return null;
            }
        }

        // 对照 WPF: private static string Translate(string text)
        //   return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
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

        // spike 版：嵌套 SshKeyViewModel（对照 WPF src/ForkPlus/UI/Dialogs/SshKeyViewModel.cs）。
        // 不引入 WPF 工程的 SshKeyViewModel.cs，避免依赖 System.Windows.*。
        // 逻辑与 WPF 版一致：KeyPath / KeyFileName / Sha256 / PublicKey / IsActive(INPC)。
        public sealed class SshKeyViewModel : System.ComponentModel.INotifyPropertyChanged
        {
            public string KeyPath { get; }
            public string KeyFileName { get; }
            public string Sha256 { get; }
            public string PublicKey { get; }

            private bool _isActive;
            public bool IsActive
            {
                get { return _isActive; }
                set
                {
                    if (_isActive != value)
                    {
                        _isActive = value;
                        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsActive)));
                    }
                }
            }

            public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

            public SshKeyViewModel(SshKey sshKey, bool isActive = false)
                : this(sshKey.FilePath, sshKey.Title, GenerateFingerprint(sshKey), sshKey.RawPublicKey, isActive)
            {
            }

            private SshKeyViewModel(string keyPath, string name, string fingerprint, string publicKey, bool isActive)
            {
                KeyPath = keyPath;
                KeyFileName = name;
                Sha256 = fingerprint;
                PublicKey = publicKey;
                _isActive = isActive;
            }

            private static string GenerateFingerprint(SshKey sshKey)
            {
                try
                {
                    string s = DistilledPublicKey(sshKey);
                    using SHA256 sha = SHA256.Create();
                    byte[] buffer = Convert.FromBase64String(s);
                    return Convert.ToBase64String(sha.ComputeHash(buffer));
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to generate fingerprint for '" + sshKey.FilePath + "'", ex);
                    return "Cannot calculate fingerprint";
                }
            }

            private static string DistilledPublicKey(SshKey sshKey)
            {
                string rawPublicKey = sshKey.RawPublicKey;
                int start = 0;
                if (rawPublicKey.StartsWith("ssh-rsa "))
                {
                    start = "ssh-rsa ".Length;
                }
                else if (rawPublicKey.StartsWith("ssh-ed25519 "))
                {
                    start = "ssh-ed25519 ".Length;
                }
                int end = rawPublicKey.Length;
                int eqIdx = rawPublicKey.LastIndexOf("==");
                if (eqIdx != -1)
                {
                    end = eqIdx + "==".Length;
                }
                else
                {
                    int spIdx = rawPublicKey.LastIndexOf(" ");
                    if (spIdx != -1)
                    {
                        end = spIdx;
                    }
                }
                return rawPublicKey.Substring(start, end - start);
            }
        }
    }
}
