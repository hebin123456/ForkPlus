using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowCheckoutBranchAsWorktreeWindowCommand.cs
    // WPF: 弹出 CheckoutBranchAsWorktreeWindow 把分支检出到新的 worktree（git worktree add）。
    public class ShowCheckoutBranchAsWorktreeWindowCommand : IUICommand
    {
        public string Id => "ShowCheckoutBranchAsWorktreeWindow";
        public string Header => ServiceLocator.Localization.Translate("Checkout as Worktree...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🌳";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new CheckoutBranchAsWorktreeWindow(repo, branch).ShowDialog() → AddWorktreeGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
