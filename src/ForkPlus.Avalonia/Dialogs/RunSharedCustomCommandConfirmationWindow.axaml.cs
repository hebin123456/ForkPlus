using System;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.12b：Avalonia 版 RunSharedCustomCommandConfirmationWindow（真实迁移版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/RunSharedCustomCommandConfirmationWindow.xaml.cs（35 行）：
    //   - public partial class RunSharedCustomCommandConfirmationWindow : ForkPlusDialogWindow
    //   - public bool TrustThisRepository => TrustThisRepositoryCheckBox.IsChecked.GetValueOrDefault()
    //   - 构造函数 (repositoryName)：
    //     * TitleTextBlock.TextTrimming/TextWrapping/MaxHeight=80
    //     * DescriptionTextBlock.TextTrimming/TextWrapping/MaxHeight=80
    //     * DialogTitle = FormatCurrent("The custom command has come from the '{0}' repository", repositoryName)
    //     * DialogDescription = Current("You should only run custom commands from trustworthy repositories. Do you really want to run it?")
    //     * SubmitButtonTitle = Current("Run")
    //     * CancelButtonTitle = Current("Cancel")
    //     * ShowCancelButton = true
    //     * Width = 600.0
    //     * ShowWarningIcon = true
    //     * TrustThisRepositoryCheckBox.Content = FormatCurrent("Trust custom commands in '{0}'", repositoryName)
    //
    // 调用方（WPF 版）：
    //   var window = new RunSharedCustomCommandConfirmationWindow(repositoryName);
    //   if (window.ShowDialog() == true) { if (window.TrustThisRepository) { ... } }
    //
    // 调用方（Avalonia 版）：
    //   var window = new RunSharedCustomCommandConfirmationWindow(repositoryName);
    //   var result = await window.ShowDialog<bool?>(owner);
    //   if (result == true) { if (window.TrustThisRepository) { ... } }
    public partial class RunSharedCustomCommandConfirmationWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // 对照 WPF: public bool TrustThisRepository => TrustThisRepositoryCheckBox.IsChecked.GetValueOrDefault()
        public bool TrustThisRepository => TrustThisRepositoryCheckBox.IsChecked.GetValueOrDefault();

        public RunSharedCustomCommandConfirmationWindow(string repositoryName)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            // 对照 WPF: base.DialogTitle / DialogDescription / SubmitButtonTitle / CancelButtonTitle
            DialogTitle = FormatCurrent("The custom command has come from the '{0}' repository", repositoryName ?? "");
            Title = DialogTitle;
            DialogDescription = Current("You should only run custom commands from trustworthy repositories. Do you really want to run it?");
            SubmitButtonTitle = Current("Run");
            CancelButtonTitle = Current("Cancel");
            ShowCancelButton = true;

            // 对照 WPF: base.Width = 600.0（WPF 在构造函数中覆盖 axaml 中的 Width=490）
            Width = 600.0;

            // 对照 WPF: base.ShowWarningIcon = true
            // spike 版基类 ShowWarningIcon 是属性，子类 axaml 中需自己放 Image。
            // 此处设为 true 仅为语义保留，spike 版不在 axaml 中放 Image（与 base 类注释一致）
            ShowWarningIcon = true;

            // 对照 WPF: TrustThisRepositoryCheckBox.Content = FormatCurrent("Trust custom commands in '{0}'", repositoryName)
            TrustThisRepositoryCheckBox.Content = FormatCurrent("Trust custom commands in '{0}'", repositoryName ?? "");
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
