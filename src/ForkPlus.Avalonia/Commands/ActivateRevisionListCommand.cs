using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ActivateRevisionListCommand.cs
    // WPF: MainWindow.ActiveRepositoryUserControl?.ActivateRevisionView / SelectAndScrollIntoView(Head) — 切换到 revision list（Ctrl+2）。
    public class ActivateRevisionListCommand : IUICommand
    {
        public string Id => "ActivateRevisionList";
        public string Header => ServiceLocator.Localization.Translate("Show All Commits", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📜";
        public string ShortcutText => "Ctrl+2";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: MainWindow.ActiveRepositoryUserControl?.ActivateRevisionView() / SelectAndScrollIntoView(Head)
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
