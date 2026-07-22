using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/SelectPreviousTabCommand.cs
    // WPF: 切换到上一个标签页。Ctrl+Shift+Tab。
    public class SelectPreviousTabCommand : IUICommand
    {
        public string Id => "SelectPreviousTab";
        public string Header => ServiceLocator.Localization.Translate("Previous Tab", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⏮";
        public string ShortcutText => "Ctrl+Shift+Tab";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: MainWindow.Instance.TabManager.SelectPreviousTab()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
