using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowGitFlowStartReleaseWindowCommand.cs
    // WPF: 弹出 GitFlowStartReleaseWindow 开始一个新的 git-flow release 分支。
    public class ShowGitFlowStartReleaseWindowCommand : IUICommand
    {
        public string Id => "ShowGitFlowStartReleaseWindow";
        public string Header => ServiceLocator.Localization.Translate("Start Release...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🌿";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new GitFlowStartReleaseWindow(repo).ShowDialog() → CreateBranchGitCommand(release/...)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
