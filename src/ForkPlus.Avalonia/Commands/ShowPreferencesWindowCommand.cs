using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowPreferencesWindowCommand.cs
    // WPF: 打开 PreferencesWindow 全局偏好设置（Ctrl+,）。
    public class ShowPreferencesWindowCommand : IUICommand
    {
        public string Id => "ShowPreferencesWindow";
        public string Header => ServiceLocator.Localization.Translate("Preferences...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⚙️";
        public string ShortcutText => "Ctrl+,";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new PreferencesWindow().ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
