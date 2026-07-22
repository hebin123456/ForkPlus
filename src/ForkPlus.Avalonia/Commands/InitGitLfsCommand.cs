using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/InitGitLfsCommand.cs
    // WPF: GitLfsInstallGitCommand.Execute(gitModule, monitor) — 初始化 Git LFS。
    public class InitGitLfsCommand : IUICommand
    {
        public string Id => "InitGitLfs";
        public string Header => ServiceLocator.Localization.Translate("Initialize Git LFS", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📦";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: GitLfsInstallGitCommand.Execute(gitModule, monitor)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
