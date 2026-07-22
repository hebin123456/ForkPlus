using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/IncreaseLayoutScaleCommand.cs
    // WPF: ForkPlusSettings.Default.LayoutScaling = Math.Min(scale + 10, 200) + RefreshLayoutScaling（Ctrl++）。
    public class IncreaseLayoutScaleCommand : IUICommand
    {
        public string Id => "IncreaseLayoutScale";
        public string Header => ServiceLocator.Localization.Translate("Zoom In", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔍";
        public string ShortcutText => "Ctrl++";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: ForkPlusSettings.Default.LayoutScaling = Math.Min(scale + 10, 200)
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
