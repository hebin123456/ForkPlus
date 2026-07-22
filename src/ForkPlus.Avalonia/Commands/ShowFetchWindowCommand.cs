using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowFetchWindowCommand.cs
    // WPF: 弹出 FetchWindow 选择远端 + 分支 + 选项进行 fetch（git fetch）。
    public class ShowFetchWindowCommand : IUICommand
    {
        public string Id => "ShowFetchWindow";
        public string Header => ServiceLocator.Localization.Translate("Fetch...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⬇";
        public string ShortcutText => "Ctrl+Shift+F";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new FetchWindow(repo, gitModule, remote).ShowDialog() → FetchGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
