using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowGitFlowStartHotfixWindowCommand.cs
    // WPF: 弹出 GitFlowStartHotfixWindow 开始一个新的 git-flow hotfix 分支。
    public class ShowGitFlowStartHotfixWindowCommand : IUICommand
    {
        public string Id => "ShowGitFlowStartHotfixWindow";
        public string Header => ServiceLocator.Localization.Translate("Start Hotfix...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🌿";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new GitFlowStartHotfixWindow(repo).ShowDialog() → CreateBranchGitCommand(hotfix/...)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
