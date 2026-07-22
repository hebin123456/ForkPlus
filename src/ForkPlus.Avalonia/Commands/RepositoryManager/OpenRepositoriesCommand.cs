using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands.RepositoryManager
{
    // 对照 WPF src/ForkPlus/UI/Commands/RepositoryManager/OpenRepositoriesCommand.cs
    // WPF: 一次性打开多个仓库（多选 + Open All）。
    public class OpenRepositoriesCommand : IUICommand
    {
        public string Id => "OpenRepositories";
        public string Header => ServiceLocator.Localization.Translate("Open All", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🗂";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 调用 TabManager.OpenRepositories(paths)
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
