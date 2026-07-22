using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands.RepositoryManager
{
    // 对照 WPF src/ForkPlus/UI/Commands/RepositoryManager/RescanRepositoriesCommand.cs
    // WPF: 弹出 RescanRepositoriesWindow，确认后扫描磁盘上的仓库列表。
    public class RescanRepositoriesCommand : IUICommand
    {
        public string Id => "RescanRepositories";
        public string Header => ServiceLocator.Localization.Translate("Rescan Repositories...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔍";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 弹出 RescanRepositoriesWindow 对话框，确认后 repositoryManager.Refresh()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
