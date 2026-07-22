using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowGitLfsTrackWindowCommand.cs
    // WPF: 弹出 GitLfsTrackWindow 配置 LFS 追踪的文件模式（git lfs track）。
    public class ShowGitLfsTrackWindowCommand : IUICommand
    {
        public string Id => "ShowGitLfsTrackWindow";
        public string Header => ServiceLocator.Localization.Translate("LFS Track...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📦";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new GitLfsTrackWindow(repo).ShowDialog() → 写入 .gitattributes
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
