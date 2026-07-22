using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ToggleAllFilesStageCommand.cs
    // WPF: 一次性 stage / unstage 所有文件（git add -A / git reset）。
    public class ToggleAllFilesStageCommand : IUICommand
    {
        public string Id => "ToggleAllFilesStage";
        public string Header => ServiceLocator.Localization.Translate("Stage/Unstage All", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "✅";
        public string ShortcutText => "Ctrl+Shift+A";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: StageAllGitCommand / UnstageAllGitCommand → UpdateRepositoryStatus
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
