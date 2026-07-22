using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowGitLfsFetchWindowCommand.cs
    // WPF: 弹出 GitLfsFetchWindow 用 Git LFS fetch 大文件（git lfs fetch）。
    public class ShowGitLfsFetchWindowCommand : IUICommand
    {
        public string Id => "ShowGitLfsFetchWindow";
        public string Header => ServiceLocator.Localization.Translate("LFS Fetch...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📦";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new GitLfsFetchWindow(repo).ShowDialog() → git lfs fetch
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
