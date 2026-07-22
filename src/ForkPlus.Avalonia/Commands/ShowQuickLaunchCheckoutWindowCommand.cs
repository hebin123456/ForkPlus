using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowQuickLaunchCheckoutWindowCommand.cs
    // WPF: 弹出 QuickLaunchCheckoutWindow 快速切换分支 / checkout revision（Quick Launch 子模式）。
    public class ShowQuickLaunchCheckoutWindowCommand : IUICommand
    {
        public string Id => "ShowQuickLaunchCheckoutWindow";
        public string Header => ServiceLocator.Localization.Translate("Quick Checkout", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⚡";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new QuickLaunchWindow(checkoutMode).ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
