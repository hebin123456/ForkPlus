using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowAbortConflictWindowCommand.cs
    // WPF: 弹出对话框确认中止 merge / rebase / cherry-pick 等操作。
    public class ShowAbortConflictWindowCommand : IUICommand
    {
        public string Id => "ShowAbortConflictWindow";
        public string Header => ServiceLocator.Localization.Translate("Abort...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🛑";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 弹出 AbortConflictWindow 对话框，确认后 AbortGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
