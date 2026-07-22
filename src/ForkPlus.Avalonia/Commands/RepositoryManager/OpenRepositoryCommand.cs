using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands.RepositoryManager
{
    // 对照 WPF src/ForkPlus/UI/Commands/RepositoryManager/OpenRepositoryCommand.cs
    // WPF: 打开 RepositoryManager 中选中的仓库（双击/回车）。检查路径存在性，缺失则提示删除引用。
    // spike: 打开仓库逻辑省略，CanExecute 始终可用。
    public class OpenRepositoryCommand : IUICommand
    {
        public string Id => "OpenRepository";
        public string Header => ServiceLocator.Localization.Translate("Open", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📂";
        public string ShortcutText => "Enter";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 通过 TabManager.OpenRepository(path) 打开仓库
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
