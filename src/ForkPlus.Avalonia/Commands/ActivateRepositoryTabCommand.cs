using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ActivateRepositoryTabCommand.cs
    // WPF: MainWindow.ActiveRepositoryUserControl?.SidebarActivateRepositoryTab() — 激活侧栏 Repository Navigator（Ctrl+Shift+1）。
    public class ActivateRepositoryTabCommand : IUICommand
    {
        public string Id => "ActivateRepositoryTab";
        public string Header => ServiceLocator.Localization.Translate("Activate Repository Navigator", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🗂";
        public string ShortcutText => "Ctrl+Shift+1";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: MainWindow.ActiveRepositoryUserControl?.SidebarActivateRepositoryTab()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
