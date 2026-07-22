using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/GitLfsPruneCommand.cs
    // WPF: GitLfsPruneGitCommand.Execute(gitModule, monitor) — LFS 清理未引用对象。
    public class GitLfsPruneCommand : IUICommand
    {
        public string Id => "GitLfsPrune";
        public string Header => ServiceLocator.Localization.Translate("Prune", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🧹";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: GitLfsPruneGitCommand.Execute(gitModule, monitor)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
