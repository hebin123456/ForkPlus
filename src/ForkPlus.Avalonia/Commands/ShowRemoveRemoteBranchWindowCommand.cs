using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowRemoveRemoteBranchWindowCommand.cs
    // WPF: 弹出 RemoveRemoteBranchWindow 删除远端分支（git push <remote> :<branch>）。
    public class ShowRemoveRemoteBranchWindowCommand : IUICommand
    {
        public string Id => "ShowRemoveRemoteBranchWindow";
        public string Header => ServiceLocator.Localization.Translate("Remove Remote Branch...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🗑";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new RemoveRemoteBranchWindow(repo, remoteBranch).ShowDialog() → DeleteRemoteBranchGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
