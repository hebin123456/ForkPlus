using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/CloseActiveTabCommand.cs
    // WPF: (Application.Current.MainWindow as MainWindow).TabManager.CloseActiveTab() — Ctrl+W / Ctrl+F4。
    public class CloseActiveTabCommand : IUICommand
    {
        public string Id => "CloseActiveTab";
        public string Header => ServiceLocator.Localization.Translate("Close Tab", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "✖";
        public string ShortcutText => "Ctrl+W";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: MainWindow.TabManager.CloseActiveTab()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
