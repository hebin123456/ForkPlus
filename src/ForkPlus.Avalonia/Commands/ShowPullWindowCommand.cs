using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowPullWindowCommand.cs
    // WPF: 弹出 PullWindow 选择远端 + 分支 + 选项进行 pull（git pull）。Ctrl+Shift+L。
    public class ShowPullWindowCommand : IUICommand
    {
        public string Id => "ShowPullWindow";
        public string Header => ServiceLocator.Localization.Translate("Pull...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⬇";
        public string ShortcutText => "Ctrl+Shift+L";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new PullWindow(repo, remoteBranch).ShowDialog() → PullGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
