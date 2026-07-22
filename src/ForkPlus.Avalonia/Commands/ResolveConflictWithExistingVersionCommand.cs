using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ResolveConflictWithExistingVersionCommand.cs
    // WPF: 用现有版本（ours / theirs）解决冲突文件。
    public class ResolveConflictWithExistingVersionCommand : IUICommand
    {
        public string Id => "ResolveConflictWithExistingVersion";
        public string Header => ServiceLocator.Localization.Translate("Resolve with Existing Version", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "✅";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 调用 ResolveConflictGitCommand(useOurs/useTheirs)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
