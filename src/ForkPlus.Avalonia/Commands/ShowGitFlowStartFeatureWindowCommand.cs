using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowGitFlowStartFeatureWindowCommand.cs
    // WPF: 弹出 GitFlowStartFeatureWindow 开始一个新的 git-flow feature 分支。
    public class ShowGitFlowStartFeatureWindowCommand : IUICommand
    {
        public string Id => "ShowGitFlowStartFeatureWindow";
        public string Header => ServiceLocator.Localization.Translate("Start Feature...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🌿";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new GitFlowStartFeatureWindow(repo).ShowDialog() → CreateBranchGitCommand(feature/...)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
