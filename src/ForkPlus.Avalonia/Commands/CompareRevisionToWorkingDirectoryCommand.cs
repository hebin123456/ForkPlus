using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/CompareRevisionToWorkingDirectoryCommand.cs
    // WPF: Application.Current.ActiveRepositoryUserControl()?.ShowRevisionDetails(WorkingDirectory(sha))。
    public class CompareRevisionToWorkingDirectoryCommand : IUICommand
    {
        public string Id => "CompareRevisionToWorkingDirectory";
        public string Header => ServiceLocator.Localization.Translate("Compare to Local Changes", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔍";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: repo?.ShowRevisionDetails(new RevisionDiffTarget.WorkingDirectory(sha))
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
