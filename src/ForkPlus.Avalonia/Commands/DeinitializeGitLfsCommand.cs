using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/DeinitializeGitLfsCommand.cs
    // WPF: GitLfsUninstallGitCommand.Execute → InvalidateAndRefresh(RepositoryData)。
    public class DeinitializeGitLfsCommand : IUICommand
    {
        public string Id => "DeinitializeGitLfs";
        public string Header => ServiceLocator.Localization.Translate("Deinitialize Git LFS", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔧";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: GitLfsUninstallGitCommand.Execute(gitModule, monitor)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
