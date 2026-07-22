using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowRevertRevisionWindowCommand.cs
    // WPF: 弹出 RevertRevisionWindow 反转指定 revision（git revert）。
    public class ShowRevertRevisionWindowCommand : IUICommand
    {
        public string Id => "ShowRevertRevisionWindow";
        public string Header => ServiceLocator.Localization.Translate("Revert...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "↩";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new RevertRevisionWindow(repo, revisions).ShowDialog() → RevertGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
