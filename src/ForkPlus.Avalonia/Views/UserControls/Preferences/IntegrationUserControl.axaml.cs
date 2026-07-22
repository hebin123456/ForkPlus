// 对照 WPF 工程 src/ForkPlus/UI/UserControls/Preferences/IntegrationUserControl.xaml.cs（128 行）：
//   - public partial class IntegrationUserControl : UserControl, ILocalizableControl
//   - 字段：ForkPlusDialogWindow _parentWindow
//   - Initialize(ForkPlusDialogWindow)：
//     * ExternalMergeToolsUserControl.Initialize(parentWindow, RevealAvailableMergeTools, MergeToolDefinitions, vars)
//     * ExternalDiffToolsUserControl.Initialize(parentWindow, RevealAvailableDiffTools, DiffToolDefinitions, vars)
//     * ShellToolComboBox.ItemsSource = 5 种 ShellTool 类型字符串
//     * 读 ForkPlusSettings.Default.ShellTool 各项
//   - ApplyLocalization：PreferencesLocalization.Apply + ExternalToolsUserControl.ApplyLocalization
//   - Save：ExternalToolManager.SaveMergeToolsSettings + SaveDiffToolsSettings + SaveShellToolSettings +
//     ShowBugtrackerLinks
//   - ShellToolComboBox_SelectionChanged：CreateShell + 填 Path/Arguments TextBox
//   - BrowseShellTool_Click：OpenDialog.SelectExecutableFile
//   - CreateShell：5 种 ShellTool 子类实例化
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF ILocalizableControl 接口 == spike 移除（spike 不做逻辑树递归翻译）
//   2. WPF ExternalMergeToolsUserControl / ExternalDiffToolsUserControl.Initialize == spike
//      移除（已迁移的 Avalonia ExternalToolsUserControl 无 Initialize 方法，spike 占位）
//   3. WPF ExternalToolManager.RevealAvailableMergeTools/DiffTools == spike 移除
//      （spike 不扫系统工具）
//   4. WPF ForkPlusDialogWindow _parentWindow == spike 用 object? 占位
//   5. WPF OpenDialog.SelectExecutableFile == spike 移除
//   6. WPF NotificationCenter.Current.RaiseShellChanged == spike 移除
//   7. WPF PreferencesLocalization.Apply == spike 移除
//   8. WPF ExternalToolManager.SaveMergeToolsSettings/SaveDiffToolsSettings == spike 移除
//      （spike 不保存外部工具设置，留 Phase 后续接入）
//   9. WPF ShellTool.* 类型 == spike 保留（Core 类型）
//  10. namespace 改为 ForkPlus.Avalonia.Views.UserControls.Preferences
using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;

namespace ForkPlus.Avalonia.Views.UserControls.Preferences
{
    public partial class IntegrationUserControl : UserControl
    {
        // 对照 WPF: private ForkPlusDialogWindow _parentWindow;
        // spike 版：用 object? 占位
        private object? _parentWindow;

        public IntegrationUserControl()
        {
            InitializeComponent();
        }

        // 对照 WPF: public void Initialize(ForkPlusDialogWindow parentWindow)
        // spike 版：parentWindow 类型改为 object?（spike 占位）
        public void Initialize(object? parentWindow)
        {
            _parentWindow = parentWindow;
            // spike: ExternalMergeToolsUserControl.Initialize(parentWindow,
            //        ExternalToolManager.RevealAvailableMergeTools(includeNonExistent: true),
            //        ExternalToolManager.MergeToolDefinitions,
            //        PreferencesLocalization.Current("Available variables: $LOCAL, $REMOTE, $BASE, $MERGED"));
            // spike 版：移除（已迁移的 Avalonia ExternalToolsUserControl 无 Initialize 方法）
            // spike: ExternalDiffToolsUserControl.Initialize(parentWindow,
            //        ExternalToolManager.RevealAvailableDiffTools(includeNonExistent: true),
            //        ExternalToolManager.DiffToolDefinitions,
            //        PreferencesLocalization.Current("Available variables: $LOCAL, $REMOTE"));
            // spike 版：移除

            string[] array = new string[5]
            {
                ShellTool.DefaultType,
                ShellTool.WindowsTerminalType,
                ShellTool.CommandPromptType,
                ShellTool.PowerShellType,
                ShellTool.CustomType
            };
            ShellToolComboBox.ItemsSource = array;
            ShellToolComboBox.SelectedItem = array.FirstItem((string x) => x == ForkPlusSettings.Default.ShellTool.Type);
            ShellToolPathTextBox.Text = ForkPlusSettings.Default.ShellTool.ApplicationPath;
            ShellToolArgumentsTextBox.Text = ForkPlusSettings.Default.ShellTool.Arguments;
            ShowBugtrackerLinksCheckBox.IsChecked = ForkPlusSettings.Default.ShowBugtrackerLinks;
        }

        // 对照 WPF: public void ApplyLocalization()
        // spike 版：移除 PreferencesLocalization.Apply + ExternalToolsUserControl.ApplyLocalization
        public void ApplyLocalization()
        {
            // spike: PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage); // 移除
            // spike: ExternalMergeToolsUserControl.ApplyLocalization(); // 移除
            // spike: ExternalDiffToolsUserControl.ApplyLocalization(); // 移除
        }

        // 对照 WPF: public void Save()
        // spike 版：移除 ExternalToolManager.SaveMergeToolsSettings/SaveDiffToolsSettings
        public void Save()
        {
            // spike: ExternalToolManager.SaveMergeToolsSettings(ExternalMergeToolsUserControl.Result); // 移除
            // spike: ExternalToolManager.SaveDiffToolsSettings(ExternalDiffToolsUserControl.Result); // 移除
            SaveShellToolSettings();
            ForkPlusSettings.Default.ShowBugtrackerLinks = ShowBugtrackerLinksCheckBox.IsChecked.GetValueOrDefault();
        }

        // 对照 WPF: private void ShellToolComboBox_SelectionChanged(...)
        private void ShellToolComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            ShellTool shellTool = CreateShell(ShellToolComboBox.SelectedItem as string, null, null);
            ShellToolPathTextBox.Text = shellTool.ApplicationPath;
            ShellToolArgumentsTextBox.Text = shellTool.Arguments;
            ShellToolArgumentsTextBox.IsEnabled = shellTool.Type == ShellTool.CustomType;
        }

        // 对照 WPF: private void BrowseShellTool_Click(object sender, RoutedEventArgs e)
        // spike 版：移除 OpenDialog.SelectExecutableFile
        private void BrowseShellTool_Click(object? sender, RoutedEventArgs e)
        {
            string initialDirectory = string.Empty;
            try
            {
                string text = ShellToolPathTextBox.Text;
                if (text != null && File.Exists(text))
                {
                    initialDirectory = Path.GetDirectoryName(text) ?? "";
                }
            }
            catch
            {
            }
            // spike: if (OpenDialog.SelectExecutableFile(_parentWindow, "Select shell", initialDirectory, out var filePath))
            //        { ShellToolPathTextBox.Text = filePath; }
            // spike 版：占位（spike 不弹文件对话框）
        }

        // 对照 WPF: private void SaveShellToolSettings()
        // spike 版：移除 NotificationCenter.Current.RaiseShellChanged
        private void SaveShellToolSettings()
        {
            string text = ShellToolPathTextBox.Text;
            string shellType = (string?)ShellToolComboBox.SelectedItem ?? ShellTool.DefaultType;
            string text2 = ShellToolArgumentsTextBox.Text;
            ForkPlusSettings.Default.ShellTool = CreateShell(shellType, text, text2);
            // spike: NotificationCenter.Current.RaiseShellChanged(this); // 移除
        }

        // 对照 WPF: private static ShellTool CreateShell(string shellType, string path, string arguments)
        private static ShellTool CreateShell(string? shellType, string? path, string? arguments)
        {
            if (shellType == ShellTool.DefaultType)
            {
                return new ShellTool.Default();
            }
            if (shellType == ShellTool.WindowsTerminalType)
            {
                if (string.IsNullOrEmpty(path))
                {
                    path = ShellTool.WindowsTerminal.TryFindInstance();
                }
                return new ShellTool.WindowsTerminal(path);
            }
            if (shellType == ShellTool.CommandPromptType)
            {
                if (string.IsNullOrEmpty(path))
                {
                    path = ShellTool.CommandPrompt.TryFindInstance();
                }
                return new ShellTool.CommandPrompt(path);
            }
            if (shellType == ShellTool.PowerShellType)
            {
                if (string.IsNullOrEmpty(path))
                {
                    path = ShellTool.PowerShell.TryFindInstance();
                }
                return new ShellTool.PowerShell(path);
            }
            return new ShellTool.Custom(path, arguments);
        }
    }
}
