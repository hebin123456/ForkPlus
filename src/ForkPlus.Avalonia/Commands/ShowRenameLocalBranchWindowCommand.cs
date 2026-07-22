using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowRenameLocalBranchWindowCommand.cs
    // WPF: 弹出 RenameLocalBranchWindow 重命名本地分支（git branch -m）。
    public class ShowRenameLocalBranchWindowCommand : IUICommand
    {
        public string Id => "ShowRenameLocalBranchWindow";
        public string Header => ServiceLocator.Localization.Translate("Rename Branch...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "✏️";
        public string ShortcutText => "F2";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new RenameLocalBranchWindow(repo, branch).ShowDialog() → RenameBranchGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
