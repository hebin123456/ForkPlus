using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/DisableImplicitRemoteFetchCommand.cs
    // WPF: DisableImplicitRemoteFetchGitCommand.Execute → InvalidateAndRefresh(Remotes)。
    public class DisableImplicitRemoteFetchCommand : IUICommand
    {
        public string Id => "DisableImplicitRemoteFetch";
        public string Header => ServiceLocator.Localization.Translate("Disable Implicit Remote Fetch", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🚫";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: DisableImplicitRemoteFetchGitCommand.Execute(gitModule, remote, disable)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
