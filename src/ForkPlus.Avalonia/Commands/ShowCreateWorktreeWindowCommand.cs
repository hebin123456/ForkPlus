using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowCreateWorktreeWindowCommand.cs
    // WPF: 弹出 CreateWorktreeWindow 创建新 worktree（git worktree add）。
    public class ShowCreateWorktreeWindowCommand : IUICommand
    {
        public string Id => "ShowCreateWorktreeWindow";
        public string Header => ServiceLocator.Localization.Translate("New Worktree...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🌳";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new CreateWorktreeWindow(repo).ShowDialog() → AddWorktreeGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
