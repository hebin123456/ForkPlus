using System;
using Avalonia.Controls;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.5b：Avalonia 版 CustomActionResultWindow（真实迁移版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/CustomActionResultWindow.xaml.cs（23 行）：
    //   - public partial class CustomActionResultWindow : ForkPlusDialogWindow
    //   - 构造函数 (customActionName, output):
    //     * InitializeComponent()
    //     * DialogTitle = customActionName
    //     * DialogDescription = PreferencesLocalization.FormatCurrent("{0} completed", customActionName)
    //     * OutputTextBox.Text = output
    //     * CancelButtonTitle = PreferencesLocalization.Current("Close")
    //     * ShowSubmitButton = false
    //
    // 调用方（WPF 版）：
    //   new CustomActionResultWindow(name, output).ShowDialog()
    //
    // 调用方（Avalonia 版）：
    //   await new CustomActionResultWindow(name, output).ShowDialog(owner)
    public partial class CustomActionResultWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        public CustomActionResultWindow(string customActionName, string output)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            // 对照 WPF: base.DialogTitle = customActionName
            string title = customActionName ?? "";
            Title = title;
            DialogTitle = title;
            // 对照 WPF: base.DialogDescription = PreferencesLocalization.FormatCurrent("{0} completed", customActionName)
            DialogDescription = FormatCurrent("{0} completed", customActionName ?? "");
            // 对照 WPF: OutputTextBox.Text = output
            OutputTextBox.Text = output ?? "";
            // 对照 WPF: base.CancelButtonTitle = PreferencesLocalization.Current("Close")
            CancelButtonTitle = Current("Close");
            // 对照 WPF: base.ShowSubmitButton = false
            ShowSubmitButton = false;
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
