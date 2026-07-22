using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowInteractiveRebaseWindowCommand.cs
    // WPF: 弹出 InteractiveRebaseWindow 对选中 revision 区间做交互式 rebase（git rebase -i）。
    public class ShowInteractiveRebaseWindowCommand : IUICommand
    {
        public string Id => "ShowInteractiveRebaseWindow";
        public string Header => ServiceLocator.Localization.Translate("Interactive Rebase...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔀";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new InteractiveRebaseWindow(repo, onto).ShowDialog() → RebaseGitCommand(interactive)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
