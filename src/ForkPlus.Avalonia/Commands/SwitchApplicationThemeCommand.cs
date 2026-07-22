using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/SwitchApplicationThemeCommand.cs
    // WPF: 切换应用主题（Light / Dark 之间 toggle，或切到指定主题）。
    public class SwitchApplicationThemeCommand : IUICommand
    {
        public string Id => "SwitchApplicationTheme";
        public string Header => ServiceLocator.Localization.Translate("Switch Theme", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🎨";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: ForkPlusSettings.Default.Theme toggle → App.RefreshWindowBorderBrush + ApplyCustomColors
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
