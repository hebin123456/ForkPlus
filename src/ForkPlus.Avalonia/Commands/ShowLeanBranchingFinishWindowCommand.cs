using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowLeanBranchingFinishWindowCommand.cs
    // WPF: 弹出 LeanBranchingFinishWindow 完成 lean branching 分支（ForkPlus 简化版 git-flow）。
    public class ShowLeanBranchingFinishWindowCommand : IUICommand
    {
        public string Id => "ShowLeanBranchingFinishWindow";
        public string Header => ServiceLocator.Localization.Translate("Finish Lean Branch...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "✅";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new LeanBranchingFinishWindow(repo, branch).ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
