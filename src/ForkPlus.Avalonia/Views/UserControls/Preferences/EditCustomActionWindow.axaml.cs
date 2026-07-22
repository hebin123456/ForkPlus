// 对照 WPF 工程 src/ForkPlus/UI/UserControls/Preferences/EditCustomActionWindow.xaml.cs（298 行）：
//   - public partial class EditCustomActionWindow : ForkPlusDialogWindow
//   - 字段：CustomCommand _customCommand / CustomCommandAction _initialAction / bool _initialized
//   - 构造函数(CustomCommand, CustomCommandAction, showCancel)：
//     * DialogTitle/DialogDescription/SubmitButtonTitle
//     * ShScriptTextBox.FontFamily = FontConstants.MonospaceFontFamily
//     * RefreshControls(target, action) 按 action 类型切换 UI
//     * CancelComboBoxItem.Show()（showCancel=true）
//   - OutAction getter：按 ComboBoxItem 选择构造 Process/Sh/Url/Cancel action
//   - CustomCommandTypeComboBox_SelectionChanged：切换 action 类型
//   - ScriptPathButton_Click：OpenDialog.SelectFile
//   - WaitForExitCheckBox_Changed：RefreshShowOutputCheckBox
//   - RefreshControls：4 种 action 类型切换 UI 显隐 + UpdateDescription
//   - UpdateDescription(Inline[] inlines)：DescriptionTextBlock.Inlines.Add(new Run(...))
//   - RefreshShowOutputCheckBox / RefreshStatus
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF ForkPlusDialogWindow 基类动态构造 chrome == spike 子类 axaml 自带 Header + Footer，
//      构造函数 SetFooter/Footer 注入
//   2. WPF Inlines（DescriptionTextBlock.Inlines.Add(new Run(...))）== spike 改用 TextBlock.Text
//      （spike 不用富文本，UpdateDescription 简化为字符串拼接）
//   3. WPF target.CreateVariablesList（WPF 扩展方法返回 Inline[]）== spike 移除
//      （spike 不显示变量列表，UpdateDescription 仅显示固定提示）
//   4. WPF OpenDialog.SelectFile == spike 移除
//   5. WPF FontConstants.MonospaceFontFamily == spike 硬编码 "Cascadia Mono,Consolas,monospace"
//      （axaml 中已设置 FontFamily）
//   6. WPF ForkPlusDialogStatus.Warning + SetStatus == spike 保留（基类已实现）
//   7. WPF CancelComboBoxItem.Show() == spike IsVisible = true
//   8. namespace 改为 ForkPlus.Avalonia.Views.UserControls.Preferences
//   9. 继承 global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Settings;
using ForkPlus.UI.CustomCommands;

namespace ForkPlus.Avalonia.Views.UserControls.Preferences
{
    public partial class EditCustomActionWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // 对照 WPF: private readonly CustomCommand _customCommand;
        private readonly CustomCommand _customCommand;

        // 对照 WPF: private readonly CustomCommandAction _initialAction;
        private readonly CustomCommandAction _initialAction;

        // 对照 WPF: private bool _initialized;
        private bool _initialized;

        // 对照 WPF: public CustomCommandAction OutAction
        public CustomCommandAction? OutAction
        {
            get
            {
                object? selectedItem = CustomCommandTypeComboBox.SelectedItem;
                if (selectedItem == ProcessComboBoxItem)
                {
                    string text = ScriptPathTextBox.Text ?? "";
                    string text2 = ArgumentsTextBox.Text ?? "";
                    bool valueOrDefault = ShowOutputCheckBox.IsChecked.GetValueOrDefault();
                    bool valueOrDefault2 = WaitForExitCheckBox.IsChecked.GetValueOrDefault();
                    return new ProcessCustomCommandAction(text, text2, valueOrDefault, valueOrDefault2);
                }
                if (selectedItem == BashComboBoxItem)
                {
                    string script = (ShScriptTextBox.Text ?? "").Replace("\r\n", "\n");
                    bool valueOrDefault3 = ShowOutputCheckBox.IsChecked.GetValueOrDefault();
                    bool valueOrDefault4 = WaitForExitCheckBox.IsChecked.GetValueOrDefault();
                    return new ShCustomCommandAction(script, valueOrDefault3, valueOrDefault4);
                }
                if (selectedItem == UrlComboBoxItem)
                {
                    return new UrlCustomCommandAction(UrlTextBox.Text ?? "");
                }
                if (selectedItem == CancelComboBoxItem)
                {
                    return new CancelCustomCommandAction();
                }
                return null;
            }
        }

        // 对照 WPF: protected override bool IsSubmitAllowed => base.IsSubmitAllowed;
        protected override bool IsSubmitAllowed => base.IsSubmitAllowed;

        // 对照 WPF: public EditCustomActionWindow(CustomCommand customCommand, CustomCommandAction action, bool showCancel)
        public EditCustomActionWindow(CustomCommand customCommand, CustomCommandAction action, bool showCancel)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);

            DialogTitle = Translate("Edit Action");
            // spike: DialogDescription 用 axaml 中的 DescriptionTextBlock2（基类 SetDescriptionTextBlock 注入）
            SetDescriptionTextBlock(DescriptionTextBlock2);
            DialogDescription = Translate("Edit custom command action");
            SubmitButtonTitle = Translate("Save");

            // spike: ShScriptTextBox.FontFamily = FontConstants.MonospaceFontFamily;
            //        axaml 中已设置 FontFamily="Cascadia Mono,Consolas,monospace"

            _customCommand = customCommand;
            _initialAction = action;
            RefreshControls(customCommand.Target, action);
            if (showCancel)
            {
                CancelComboBoxItem.IsVisible = true;
            }
            _initialized = true;
        }

        // 对照 WPF: private void CustomCommandTypeComboBox_SelectionChanged(...)
        private void CustomCommandTypeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_initialized) return;
            object? selectedItem = CustomCommandTypeComboBox.SelectedItem;
            CustomCommandAction? action;
            if (selectedItem == ProcessComboBoxItem)
            {
                action = (_initialAction as ProcessCustomCommandAction) ?? new ProcessCustomCommandAction("${git}", "", showOutput: true, waitForExit: true);
            }
            else if (selectedItem == BashComboBoxItem)
            {
                action = (_initialAction as ShCustomCommandAction) ?? new ShCustomCommandAction(ShCustomCommandAction.DefaultScript(_customCommand.Target), showOutput: true, waitForExit: true);
            }
            else if (selectedItem == UrlComboBoxItem)
            {
                action = (_initialAction as UrlCustomCommandAction) ?? new UrlCustomCommandAction("https://hebin.me");
            }
            else if (selectedItem == CancelComboBoxItem)
            {
                action = (_initialAction as CancelCustomCommandAction) ?? new CancelCustomCommandAction();
            }
            else
            {
                return;
            }
            RefreshControls(_customCommand.Target, action!);
        }

        // 对照 WPF: private void WaitForExitCheckBox_Changed(...)
        private void WaitForExitCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            RefreshShowOutputCheckBox();
        }

        // 对照 WPF: private void ScriptPathTextBox_TextChanged(...)
        private void ScriptPathTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            RefreshStatus();
        }

        // 对照 WPF: private void ScriptPathButton_Click(...)
        // spike 版：移除 OpenDialog.SelectFile
        private void ScriptPathButton_Click(object? sender, RoutedEventArgs e)
        {
            // spike: string initialDirectory = RepositoryManager.Instance.DefaultSourceDir();
            //        if (OpenDialog.SelectFile(this, "Select File", initialDirectory, "Executable files", "*.bat; *.exe; *.cmd", out var filePath))
            //        { ScriptPathTextBox.Text = filePath; }
            // spike 版：占位（spike 不弹文件对话框）
        }

        // 对照 WPF: private void RefreshControls(CustomCommandTarget target, CustomCommandAction action)
        // spike 版：4 种 action 类型切换 UI 显隐，移除 inlines（spike 不显示变量列表）
        private void RefreshControls(CustomCommandTarget target, CustomCommandAction action)
        {
            // spike 版：inlines 始终为空（WPF 用 target.CreateVariablesList，spike 不迁移）
            if (action is ProcessCustomCommandAction processCustomCommandAction)
            {
                if (!_initialized)
                {
                    CustomCommandTypeComboBox.SelectedItem = ProcessComboBoxItem;
                }
                ScriptPathTextBox.Text = processCustomCommandAction.Path;
                ArgumentsTextBox.Text = processCustomCommandAction.Parameters;
                ShowOutputCheckBox.IsChecked = processCustomCommandAction.ShowOutput;
                WaitForExitCheckBox.IsChecked = processCustomCommandAction.WaitForExit;
                UrlContainer.IsVisible = false;
                ProcessCustomCommandContainer.IsVisible = true;
                ShScriptContainer.IsVisible = false;
                WaitForExitCheckBox.IsVisible = true;
                ShowOutputCheckBox.IsVisible = true;
            }
            else if (action is ShCustomCommandAction shCustomCommandAction)
            {
                if (!_initialized)
                {
                    CustomCommandTypeComboBox.SelectedItem = BashComboBoxItem;
                }
                ShScriptTextBox.Text = shCustomCommandAction.Script;
                ShowOutputCheckBox.IsChecked = shCustomCommandAction.ShowOutput;
                WaitForExitCheckBox.IsChecked = shCustomCommandAction.WaitForExit;
                UrlContainer.IsVisible = false;
                ProcessCustomCommandContainer.IsVisible = false;
                ShScriptContainer.IsVisible = true;
                WaitForExitCheckBox.IsVisible = true;
                ShowOutputCheckBox.IsVisible = true;
            }
            else if (action is UrlCustomCommandAction urlCustomCommandAction)
            {
                if (!_initialized)
                {
                    CustomCommandTypeComboBox.SelectedItem = UrlComboBoxItem;
                }
                UrlTextBox.Text = urlCustomCommandAction.Url;
                UrlContainer.IsVisible = true;
                ProcessCustomCommandContainer.IsVisible = false;
                ShScriptContainer.IsVisible = false;
                WaitForExitCheckBox.IsVisible = false;
                ShowOutputCheckBox.IsVisible = false;
            }
            else if (action is CancelCustomCommandAction)
            {
                if (!_initialized)
                {
                    CustomCommandTypeComboBox.SelectedItem = CancelComboBoxItem;
                }
                UrlContainer.IsVisible = false;
                ProcessCustomCommandContainer.IsVisible = false;
                ShScriptContainer.IsVisible = false;
                WaitForExitCheckBox.IsVisible = false;
                ShowOutputCheckBox.IsVisible = false;
            }
            UpdateDescription();
        }

        // 对照 WPF: private void UpdateDescription(Inline[] inlines)
        // spike 版：改用 TextBlock.Text（WPF 用 Inlines），spike 不显示变量列表
        private void UpdateDescription()
        {
            // spike: WPF 用 DescriptionTextBlock.Inlines.Add(new Run(...)) 显示变量列表
            //        spike 版仅显示固定提示（spike 不迁移 target.CreateVariablesList）
            DescriptionTextBlock.Text = PreferencesLocalization.Translate("Available variables:", ForkPlusSettings.Default.UiLanguage);
        }

        // 对照 WPF: private static string Translate(string text)
        private static string Translate(string text)
        {
            return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
        }

        // 对照 WPF: private void RefreshShowOutputCheckBox()
        private void RefreshShowOutputCheckBox()
        {
            if (!WaitForExitCheckBox.IsChecked.GetValueOrDefault())
            {
                ShowOutputCheckBox.IsChecked = false;
                ShowOutputCheckBox.IsEnabled = false;
            }
            else
            {
                ShowOutputCheckBox.IsEnabled = true;
            }
        }

        // 对照 WPF: private void RefreshStatus()
        private void RefreshStatus()
        {
            string text = ScriptPathTextBox.Text ?? "";
            text = Environment.ExpandEnvironmentVariables(text);
            try
            {
                if (text != "${git}" && text != "$git" && text != "${sh}" && text != "$sh" && !System.IO.File.Exists(text))
                {
                    SetStatus(global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogStatus.Warning, "Script path not found");
                    return;
                }
            }
            catch
            {
                SetStatus(global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogStatus.Warning, "Script path not found");
                return;
            }
            SetStatus(global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogStatus.None, string.Empty);
        }
    }
}
