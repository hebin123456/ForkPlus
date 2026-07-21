using System;
using Avalonia.Controls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.2b：Avalonia 版 MessageBoxWindow（真实迁移版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/MessageBoxWindow.xaml.cs（36 行）：
    //   - public partial class MessageBoxWindow : ForkPlusDialogWindow
    //   - 构造函数 (title, description, submitTitle, cancelTitle="Cancel",
    //     showCancelButton=true, width=600.0, showWarningIcon=false):
    //     * InitializeComponent()
    //     * TitleTextBlock.TextTrimming/TextWrapping/MaxHeight
    //     * DescriptionTextBlock.TextTrimming/TextWrapping/MaxHeight
    //     * DialogTitle = Translate(title)
    //     * DialogDescription = Translate(description)
    //     * SubmitButtonTitle = Translate(submitTitle)
    //     * CancelButtonTitle = Translate(cancelTitle)
    //     * ShowCancelButton = showCancelButton
    //     * Width = width
    //     * ShowWarningIcon = showWarningIcon
    //   - private static string Translate(string text):
    //     return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
    //
    // 调用方（WPF 版）：
    //   var window = new MessageBoxWindow("Confirm", "Delete file?", "Yes", "No");
    //   if (window.ShowDialog() == true) { ... }
    //
    // 调用方（Avalonia 版）：
    //   var window = new MessageBoxWindow("Confirm", "Delete file?", "Yes", "No");
    //   var result = await window.ShowDialog<bool?>(owner);
    //   if (result == true) { ... }
    public partial class MessageBoxWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        public MessageBoxWindow(
            string title,
            string description,
            string submitTitle,
            string cancelTitle = "Cancel",
            bool showCancelButton = true,
            double width = 600.0,
            bool showWarningIcon = false)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            // 对照 WPF: base.DialogTitle = Translate(title)
            Title = Translate(title);
            DialogTitle = Translate(title);
            DialogDescription = Translate(description);
            SubmitButtonTitle = Translate(submitTitle);
            CancelButtonTitle = Translate(cancelTitle);
            ShowCancelButton = showCancelButton;
            Width = width;
            ShowWarningIcon = showWarningIcon;
        }

        // 对照 WPF: private static string Translate(string text)
        //   return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
        // Phase 0.4b 完成后，ILocalizationService 已注册到 ServiceLocator
        // 通过 ServiceLocator.Localization.Translate(text, UiLanguage) 取本地化文本
        private static string Translate(string text)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.Translate(text, userSettings.UiLanguage);
            }
            // ServiceLocator 未初始化（设计时/测试时）回退到原文本
            return text;
        }
    }
}
