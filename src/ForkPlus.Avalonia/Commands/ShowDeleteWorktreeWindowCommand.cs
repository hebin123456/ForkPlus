using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowDeleteWorktreeWindowCommand.cs
    // WPF: 弹出 DeleteWorktreeWindow 确认删除 worktree（git worktree remove）。
    public class ShowDeleteWorktreeWindowCommand : IUICommand
    {
        public string Id => "ShowDeleteWorktreeWindow";
        public string Header => ServiceLocator.Localization.Translate("Delete Worktree...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🗑";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new DeleteWorktreeWindow(repo, worktree).ShowDialog() → RemoveWorktreeGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
