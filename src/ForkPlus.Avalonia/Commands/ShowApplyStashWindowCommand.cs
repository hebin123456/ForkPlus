using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowApplyStashWindowCommand.cs
    // WPF: 弹出 ApplyStashWindow 应用选中的 stash（git stash apply）。
    public class ShowApplyStashWindowCommand : IUICommand
    {
        public string Id => "ShowApplyStashWindow";
        public string Header => ServiceLocator.Localization.Translate("Apply Stash...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📥";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new ApplyStashWindow(repo, stash).ShowDialog() → ApplyStashGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
