using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/GitLfsUnlockCommand.cs
    // WPF: GitLfsUnlockGitCommand.Execute(gitModule, filePaths, monitor) — LFS 解锁文件。
    public class GitLfsUnlockCommand : IUICommand
    {
        public string Id => "GitLfsUnlock";
        public string Header => ServiceLocator.Localization.Translate("Unlock", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔓";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: GitLfsUnlockGitCommand.Execute(gitModule, filePaths, monitor)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
