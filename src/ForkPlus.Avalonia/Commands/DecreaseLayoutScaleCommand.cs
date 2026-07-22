using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/DecreaseLayoutScaleCommand.cs
    // WPF: ForkPlusSettings.Default.LayoutScaling = Math.Max(scale - 10, 100) + RefreshLayoutScaling（Ctrl+-）。
    public class DecreaseLayoutScaleCommand : IUICommand
    {
        public string Id => "DecreaseLayoutScale";
        public string Header => ServiceLocator.Localization.Translate("Zoom Out", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔍";
        public string ShortcutText => "Ctrl+-";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: ForkPlusSettings.Default.LayoutScaling = Math.Max(scale - 10, 100)
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
