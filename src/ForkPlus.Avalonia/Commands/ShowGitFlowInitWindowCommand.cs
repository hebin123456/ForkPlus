using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowGitFlowInitWindowCommand.cs
    // WPF: 弹出 GitFlowInitWindow 初始化 git-flow 配置（master/develop/feature/...）。
    public class ShowGitFlowInitWindowCommand : IUICommand
    {
        public string Id => "ShowGitFlowInitWindow";
        public string Header => ServiceLocator.Localization.Translate("Initialize Git Flow...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🌊";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new GitFlowInitWindow(repo).ShowDialog() → 写入 git-flow 配置
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
