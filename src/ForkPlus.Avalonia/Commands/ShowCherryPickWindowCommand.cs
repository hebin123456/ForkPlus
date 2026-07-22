using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowCherryPickWindowCommand.cs
    // WPF: 弹出 CherryPickWindow cherry-pick 选中的 revision（git cherry-pick）。
    public class ShowCherryPickWindowCommand : IUICommand
    {
        public string Id => "ShowCherryPickWindow";
        public string Header => ServiceLocator.Localization.Translate("Cherry Pick...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🍒";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new CherryPickWindow(repo, revisions).ShowDialog() → CherryPickGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
