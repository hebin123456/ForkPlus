using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowRemoveLocalBranchWindowCommand.cs
    // WPF: 弹出 RemoveLocalBranchWindow 删除本地分支（git branch -d/-D）。
    public class ShowRemoveLocalBranchWindowCommand : IUICommand
    {
        public string Id => "ShowRemoveLocalBranchWindow";
        public string Header => ServiceLocator.Localization.Translate("Remove Branch...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🗑";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new RemoveLocalBranchWindow(repo, branch).ShowDialog() → DeleteBranchGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
