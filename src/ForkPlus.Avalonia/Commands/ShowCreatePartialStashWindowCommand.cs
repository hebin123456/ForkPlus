using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowCreatePartialStashWindowCommand.cs
    // WPF: 弹出 CreatePartialStashWindow 选择部分文件创建 stash（git stash push -- <paths>）。
    public class ShowCreatePartialStashWindowCommand : IUICommand
    {
        public string Id => "ShowCreatePartialStashWindow";
        public string Header => ServiceLocator.Localization.Translate("Stash Selected Files...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📦";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new CreatePartialStashWindow(repo, files).ShowDialog() → StashGitCommand(partial)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
