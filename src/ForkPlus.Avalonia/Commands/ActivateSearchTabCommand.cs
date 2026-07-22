using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ActivateSearchTabCommand.cs
    // WPF: MainWindow.ActiveRepositoryUserControl?.SidebarActivateSearchTab() — 激活侧栏 Search Navigator（Ctrl+Shift+2）。
    public class ActivateSearchTabCommand : IUICommand
    {
        public string Id => "ActivateSearchTab";
        public string Header => ServiceLocator.Localization.Translate("Activate Search Navigator", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔍";
        public string ShortcutText => "Ctrl+Shift+2";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: MainWindow.ActiveRepositoryUserControl?.SidebarActivateSearchTab()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
