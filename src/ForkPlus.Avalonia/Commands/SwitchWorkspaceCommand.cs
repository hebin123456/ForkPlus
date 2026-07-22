using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/SwitchWorkspaceCommand.cs
    // WPF: 切换到指定 workspace（工作区分组）。
    public class SwitchWorkspaceCommand : IUICommand
    {
        public string Id => "SwitchWorkspace";
        public string Header => ServiceLocator.Localization.Translate("Switch Workspace", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🗂";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: ForkPlusSettings.Default.ActiveWorkspace = workspace → RefreshRepositoryManager
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
