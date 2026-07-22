using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/DeinitializeGitFlowCommand.cs
    // WPF: DeinitializeGitFlowGitCommand.Execute → InvalidateAndRefresh(GitFlowSettings|References)。
    public class DeinitializeGitFlowCommand : IUICommand
    {
        public string Id => "DeinitializeGitFlow";
        public string Header => ServiceLocator.Localization.Translate("Deinitialize Git Flow", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔧";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: DeinitializeGitFlowGitCommand.Execute(gitModule, monitor)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
