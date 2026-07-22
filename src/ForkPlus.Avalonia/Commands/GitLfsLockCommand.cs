using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/GitLfsLockCommand.cs
    // WPF: GitLfsLockGitCommand.Execute(filePaths) — LFS 锁定文件。
    public class GitLfsLockCommand : IUICommand
    {
        public string Id => "GitLfsLock";
        public string Header => ServiceLocator.Localization.Translate("Lock", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔒";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: GitLfsLockGitCommand.Execute(gitModule, filePaths, monitor)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
