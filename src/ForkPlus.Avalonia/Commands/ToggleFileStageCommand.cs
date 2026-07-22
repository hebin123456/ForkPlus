using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ToggleFileStageCommand.cs
    // WPF: stage / unstage 选中的文件（Enter 或 Ctrl+Shift+S）。
    public class ToggleFileStageCommand : IUICommand
    {
        public string Id => "ToggleFileStage";
        public string Header => ServiceLocator.Localization.Translate("Stage/Unstage File", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "↔";
        public string ShortcutText => "Ctrl+Shift+S";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: StageFileGitCommand / UnstageGitCommand → UpdateRepositoryStatus
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
