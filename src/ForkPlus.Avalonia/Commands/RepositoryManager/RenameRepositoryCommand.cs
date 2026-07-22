using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands.RepositoryManager
{
    // 对照 WPF src/ForkPlus/UI/Commands/RepositoryManager/RenameRepositoryCommand.cs
    // WPF: 进入 RepositoryManagerRepositoryItem 的编辑模式（F2 / 双击名称）。
    public class RenameRepositoryCommand : IUICommand
    {
        public string Id => "RenameRepository";
        public string Header => ServiceLocator.Localization.Translate("Rename", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "✏️";
        public string ShortcutText => "F2";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 设置 itemToRename.IsInEditMode = true
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
