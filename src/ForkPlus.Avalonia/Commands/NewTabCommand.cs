using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/NewTabCommand.cs
    // WPF: 新建一个 Repository Manager 标签页（Ctrl+T）。
    public class NewTabCommand : IUICommand
    {
        public string Id => "NewTab";
        public string Header => ServiceLocator.Localization.Translate("New Tab", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "➕";
        public string ShortcutText => "Ctrl+T";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: MainWindow.Instance.TabManager.NewTab()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
