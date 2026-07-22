using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowSaveStashWindowCommand.cs
    // WPF: 弹出 SaveStashWindow 创建新 stash（git stash push）。
    public class ShowSaveStashWindowCommand : IUICommand
    {
        public string Id => "ShowSaveStashWindow";
        public string Header => ServiceLocator.Localization.Translate("Stash...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📦";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new SaveStashWindow(repo).ShowDialog() → StashGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
