using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/CreatePullRequestCommand.cs
    // WPF: push 后在浏览器打开 PR URL（PushGitCommand + Uri.OpenInBrowser）。
    public class CreatePullRequestCommand : IUICommand
    {
        public string Id => "CreatePullRequest";
        public string Header => ServiceLocator.Localization.Translate("Create Pull Request", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔗";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: PushGitCommand.Execute → Uri(pullRequestUrl).OpenInBrowser()
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
