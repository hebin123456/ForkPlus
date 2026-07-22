using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowGitLfsPullWindowCommand.cs
    // WPF: 弹出 GitLfsPullWindow 用 Git LFS pull 大文件（git lfs pull）。
    public class ShowGitLfsPullWindowCommand : IUICommand
    {
        public string Id => "ShowGitLfsPullWindow";
        public string Header => ServiceLocator.Localization.Translate("LFS Pull...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📦";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new GitLfsPullWindow(repo).ShowDialog() → git lfs pull
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
