using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowGitLfsStatusWindowCommand.cs
    // WPF: 弹出 GitLfsStatusWindow 显示 Git LFS 文件状态（git lfs status）。
    public class ShowGitLfsStatusWindowCommand : IUICommand
    {
        public string Id => "ShowGitLfsStatusWindow";
        public string Header => ServiceLocator.Localization.Translate("LFS Status...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📦";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new GitLfsStatusWindow(repo).ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
