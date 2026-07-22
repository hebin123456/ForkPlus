using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowLeanBranchingStartWindowCommand.cs
    // WPF: 弹出 LeanBranchingStartWindow 开始一个新的 lean branching 分支。
    public class ShowLeanBranchingStartWindowCommand : IUICommand
    {
        public string Id => "ShowLeanBranchingStartWindow";
        public string Header => ServiceLocator.Localization.Translate("Start Lean Branch...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🌿";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new LeanBranchingStartWindow(repo).ShowDialog() → CreateBranchGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
