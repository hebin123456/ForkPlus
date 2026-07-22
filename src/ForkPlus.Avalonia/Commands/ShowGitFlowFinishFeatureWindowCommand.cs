using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowGitFlowFinishFeatureWindowCommand.cs
    // WPF: 弹出 GitFlowFinishFeatureWindow 完成 git-flow feature 分支。
    public class ShowGitFlowFinishFeatureWindowCommand : IUICommand
    {
        public string Id => "ShowGitFlowFinishFeatureWindow";
        public string Header => ServiceLocator.Localization.Translate("Finish Feature...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "✅";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new GitFlowFinishFeatureWindow(repo, featureBranch).ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
