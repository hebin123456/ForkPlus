using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowConfigureWorkspacesWindowCommand.cs
    // WPF: 打开 ConfigureWorkspacesWindow 管理工作区（workspace）分组。
    public class ShowConfigureWorkspacesWindowCommand : IUICommand
    {
        public string Id => "ShowConfigureWorkspacesWindow";
        public string Header => ServiceLocator.Localization.Translate("Configure Workspaces...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🗂";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new ConfigureWorkspacesWindow().ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
