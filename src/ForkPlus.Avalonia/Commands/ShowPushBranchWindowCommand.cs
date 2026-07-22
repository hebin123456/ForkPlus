using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowPushBranchWindowCommand.cs
    // WPF: 弹出 PushWindow push 指定本地分支到远端（git push <remote> <branch>）。
    public class ShowPushBranchWindowCommand : IUICommand
    {
        public string Id => "ShowPushBranchWindow";
        public string Header => ServiceLocator.Localization.Translate("Push Branch...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⬆";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new PushWindow(repo, null, localBranch).ShowDialog() → PushGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
