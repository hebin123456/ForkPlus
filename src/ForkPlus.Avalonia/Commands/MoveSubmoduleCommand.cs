using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/MoveSubmoduleCommand.cs
    // WPF: MoveSubmoduleGitCommand.Execute + RenameGitmodulesSectionGitCommand — 移动子模块到新路径。
    public class MoveSubmoduleCommand : IUICommand
    {
        public string Id => "MoveSubmodule";
        public string Header => ServiceLocator.Localization.Translate("Move...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📁";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: MoveSubmoduleGitCommand.Execute(gitModule, oldPath, newPath, monitor)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
