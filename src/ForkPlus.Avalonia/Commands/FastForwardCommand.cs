using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/FastForwardCommand.cs
    // WPF: FastForwardGitCommand.Execute(localBranch) → UpdateSubmodulesGitCommand → InvalidateAndRefresh(References)。
    public class FastForwardCommand : IUICommand
    {
        public string Id => "FastForward";
        public string Header => ServiceLocator.Localization.Translate("Fast-Forward", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⏩";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: FastForwardGitCommand.Execute(gitModule, localBranch, monitor)
            //        → UpdateSubmodulesGitCommand → InvalidateAndRefresh(References)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
