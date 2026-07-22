using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowMergeBranchWindowCommand.cs
    // WPF: 弹出 MergeBranchWindow 合并指定分支到当前分支（git merge）。
    public class ShowMergeBranchWindowCommand : IUICommand
    {
        public string Id => "ShowMergeBranchWindow";
        public string Header => ServiceLocator.Localization.Translate("Merge...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔀";
        public string ShortcutText => "Ctrl+Shift+M";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new MergeBranchWindow(repo).ShowDialog() → MergeGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
