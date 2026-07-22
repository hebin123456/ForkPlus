using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ToggleDisableRefreshOnActivateCommand.cs
    // WPF: 切换 "切换标签页时不自动刷新" 偏好开关。
    public class ToggleDisableRefreshOnActivateCommand : IUICommand
    {
        public string Id => "ToggleDisableRefreshOnActivate";
        public string Header => ServiceLocator.Localization.Translate("Disable Refresh on Activate", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔄";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: ForkPlusSettings.Default.DisableRefreshOnActivate toggle
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
