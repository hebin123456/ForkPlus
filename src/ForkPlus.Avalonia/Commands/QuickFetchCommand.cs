using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/QuickFetchCommand.cs
    // WPF: 快速 fetch（用默认 remote，无对话框）。Ctrl+Alt+Shift+F。
    public class QuickFetchCommand : IUICommand
    {
        public string Id => "QuickFetch";
        public string Header => ServiceLocator.Localization.Translate("Quick Fetch", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⬇";
        public string ShortcutText => "Ctrl+Alt+Shift+F";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 取默认 remote → JobQueue.Add(FetchGitCommand) → InvalidateAndRefresh(Revisions|References)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
