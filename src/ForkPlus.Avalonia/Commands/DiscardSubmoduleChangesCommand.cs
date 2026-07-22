using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/DiscardSubmoduleChangesCommand.cs
    // WPF: 确认后 DiscardAllSubmoduleChangesGitCommand.Execute → RefreshFileStatusCommand。
    public class DiscardSubmoduleChangesCommand : IUICommand
    {
        public string Id => "DiscardSubmoduleChanges";
        public string Header => ServiceLocator.Localization.Translate("Discard submodule changes...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🗑";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: MessageBoxWindow 确认 → DiscardAllSubmoduleChangesGitCommand.Execute → RefreshFileStatusCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
