using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/FastForwardPullCommand.cs
    // WPF: FastForwardPullGitCommand.Execute(remoteBranch, localBranch) → UpdateSubmodulesGitCommand。
    public class FastForwardPullCommand : IUICommand
    {
        public string Id => "FastForwardPull";
        public string Header => ServiceLocator.Localization.Translate("Fast-Forward Pull", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⏩";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: FastForwardPullGitCommand.Execute(gitModule, remoteBranch, localBranch, monitor)
            //        → UpdateSubmodulesGitCommand → InvalidateAndRefresh
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
