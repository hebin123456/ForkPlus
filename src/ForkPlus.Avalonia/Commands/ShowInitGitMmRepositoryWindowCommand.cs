using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowInitGitMmRepositoryWindowCommand.cs
    // WPF: 弹出 InitGitMmRepositoryWindow 初始化 git-mm 仓库（ForkPlus 自有的子仓库管理）。
    public class ShowInitGitMmRepositoryWindowCommand : IUICommand
    {
        public string Id => "ShowInitGitMmRepositoryWindow";
        public string Header => ServiceLocator.Localization.Translate("Initialize Git MM Repository...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🧩";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new InitGitMmRepositoryWindow(repo).ShowDialog() → git mm init
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
