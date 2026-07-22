using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowInitRepositoryWindowCommand.cs
    // WPF: 弹出 InitRepositoryWindow 在指定目录初始化新仓库（git init）。
    public class ShowInitRepositoryWindowCommand : IUICommand
    {
        public string Id => "ShowInitRepositoryWindow";
        public string Header => ServiceLocator.Localization.Translate("New Repository...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "➕";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new InitRepositoryWindow().ShowDialog() → InitRepositoryGitCommand → TabManager.OpenRepository
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
