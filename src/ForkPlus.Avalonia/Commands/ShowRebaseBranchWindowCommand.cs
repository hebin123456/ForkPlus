using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowRebaseBranchWindowCommand.cs
    // WPF: 弹出 RebaseBranchWindow 把当前分支 rebase 到指定分支（git rebase）。
    public class ShowRebaseBranchWindowCommand : IUICommand
    {
        public string Id => "ShowRebaseBranchWindow";
        public string Header => ServiceLocator.Localization.Translate("Rebase...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔀";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new RebaseBranchWindow(repo).ShowDialog() → RebaseGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
