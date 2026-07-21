using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.6b：Avalonia 版 OpenRepositoryAlertWindow（真实迁移版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/OpenRepositoryAlertWindow.xaml.cs（36 行）：
    //   - public partial class OpenRepositoryAlertWindow : ForkPlusDialogWindow
    //   - 构造函数 (repositoryDirectory):
    //     * InitializeComponent()
    //     * DescriptionTextBlock.TextTrimming + MaxHeight
    //     * DialogTitle = PreferencesLocalization.Current("The directory is not under git source control")
    //     * DialogDescription = PreferencesLocalization.FormatCurrent("The '{0}' directory is not a git repository", repositoryDirectory)
    //     * ShowSubmitButton = false
    //     * FirstButton.Content = "Initialize git repository here"
    //     * CancelButtonTitle = "Close"
    //     * Footer.CancelButton.IsDefault = true
    //     * Footer.CancelButton.Focus()
    //   - FirstButton_Click: CloseWithOk()
    //
    // 调用方（WPF 版）：
    //   var window = new OpenRepositoryAlertWindow(repoDir);
    //   if (window.ShowDialog() == true) { /* 用户点 Initialize */ }
    //
    // 调用方（Avalonia 版）：
    //   var window = new OpenRepositoryAlertWindow(repoDir);
    //   var result = await window.ShowDialog<bool?>(owner);
    //   if (result == true) { /* 用户点 Initialize */ }
    public partial class OpenRepositoryAlertWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        public OpenRepositoryAlertWindow(string repositoryDirectory)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            // 对照 WPF: base.DialogTitle / DialogDescription
            string title = Current("The directory is not under git source control");
            Title = title;
            DialogTitle = title;
            DialogDescription = FormatCurrent("The '{0}' directory is not a git repository", repositoryDirectory ?? "");

            // 对照 WPF: base.ShowSubmitButton = false（只显示 Cancel + 自定义 FirstButton）
            ShowSubmitButton = false;

            // 对照 WPF: FirstButton.Content = PreferencesLocalization.Current("Initialize git repository here")
            FirstButton.Content = Current("Initialize git repository here");

            // 对照 WPF: base.CancelButtonTitle = "Close"
            CancelButtonTitle = Current("Close");

            // 对照 WPF: Footer.CancelButton.IsDefault = true + Footer.CancelButton.Focus()
            // Avalonia 11 的 Button.IsDefault 是 styled property
            if (Footer?.CancelButton != null)
            {
                Footer.CancelButton.IsDefault = true;
                // Focus 需要在窗口 Loaded 后调用，否则控件尚未完成布局
                Dispatcher.UIThread.Post(() =>
                {
                    try { Footer.CancelButton.Focus(); }
                    catch { /* 控件可能已释放 */ }
                });
            }
        }

        // 对照 WPF: private void FirstButton_Click(object sender, RoutedEventArgs e) { CloseWithOk(); }
        private void FirstButton_Click(object sender, RoutedEventArgs e)
        {
            CloseWithOk();
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
