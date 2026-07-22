using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/OpenIssueTrackerCommand.cs
    // WPF: 在浏览器中打开 ForkPlus 的 Issue Tracker。
    public class OpenIssueTrackerCommand : IUICommand
    {
        public string Id => "OpenIssueTracker";
        public string Header => ServiceLocator.Localization.Translate("Report an Issue", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🐞";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new Uri(issueTrackerUrl).OpenInBrowser()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
