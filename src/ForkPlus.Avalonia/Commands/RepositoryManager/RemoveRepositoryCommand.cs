using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands.RepositoryManager
{
    // 对照 WPF src/ForkPlus/UI/Commands/RepositoryManager/RemoveRepositoryCommand.cs
    // WPF: 从 RepositoryManager 移除选中的仓库或文件夹（带确认对话框）。
    public class RemoveRepositoryCommand : IUICommand
    {
        public string Id => "RemoveRepository";
        public string Header => ServiceLocator.Localization.Translate("Remove...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🗑";
        public string ShortcutText => "Delete";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 弹出确认对话框 → RepositoryManager.Instance.DeleteRepositories/DeleteFolders
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
