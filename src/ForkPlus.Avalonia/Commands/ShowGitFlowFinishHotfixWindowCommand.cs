using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowGitFlowFinishHotfixWindowCommand.cs
    // WPF: 弹出 GitFlowFinishHotfixWindow 完成 git-flow hotfix 分支。
    public class ShowGitFlowFinishHotfixWindowCommand : IUICommand
    {
        public string Id => "ShowGitFlowFinishHotfixWindow";
        public string Header => ServiceLocator.Localization.Translate("Finish Hotfix...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "✅";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new GitFlowFinishHotfixWindow(repo, hotfixBranch).ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
