using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/UpdateApplicationCommand.cs
    // WPF: 检查并下载应用更新（UpdateCheckManager + UpdateAvailableWindow）。
    public class UpdateApplicationCommand : IUICommand
    {
        public string Id => "UpdateApplication";
        public string Header => ServiceLocator.Localization.Translate("Check for Updates...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⬆";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: UpdateCheckManager.Instance.CheckAndShowUpdate()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
