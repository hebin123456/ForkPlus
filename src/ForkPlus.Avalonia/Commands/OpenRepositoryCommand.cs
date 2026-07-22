using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/OpenRepositoryCommand.cs
    // WPF: 弹出目录选择对话框，打开/初始化一个仓库（Ctrl+O）。
    public class OpenRepositoryCommand : IUICommand
    {
        public string Id => "OpenRepository";
        public string Header => ServiceLocator.Localization.Translate("Open Repository...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📂";
        public string ShortcutText => "Ctrl+O";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: OpenDialog.SelectDirectory → TabManager.OpenRepository / InitRepositoryGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
