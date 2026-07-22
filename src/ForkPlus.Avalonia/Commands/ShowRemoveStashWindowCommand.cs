using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowRemoveStashWindowCommand.cs
    // WPF: 弹出 RemoveStashWindow 确认删除 stash（git stash drop）。
    public class ShowRemoveStashWindowCommand : IUICommand
    {
        public string Id => "ShowRemoveStashWindow";
        public string Header => ServiceLocator.Localization.Translate("Remove Stash...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🗑";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new RemoveStashWindow(repo, stash).ShowDialog() → DropStashGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
