using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/SelectNextTabCommand.cs
    // WPF: 切换到下一个标签页。Ctrl+Tab。
    public class SelectNextTabCommand : IUICommand
    {
        public string Id => "SelectNextTab";
        public string Header => ServiceLocator.Localization.Translate("Next Tab", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⏭";
        public string ShortcutText => "Ctrl+Tab";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: MainWindow.Instance.TabManager.SelectNextTab()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
