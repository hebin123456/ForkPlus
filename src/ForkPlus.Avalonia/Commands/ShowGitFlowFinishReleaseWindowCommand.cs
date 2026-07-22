using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowGitFlowFinishReleaseWindowCommand.cs
    // WPF: 弹出 GitFlowFinishReleaseWindow 完成 git-flow release 分支。
    public class ShowGitFlowFinishReleaseWindowCommand : IUICommand
    {
        public string Id => "ShowGitFlowFinishReleaseWindow";
        public string Header => ServiceLocator.Localization.Translate("Finish Release...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "✅";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new GitFlowFinishReleaseWindow(repo, releaseBranch).ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
