using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ExitApplicationCommand.cs
    // WPF: Application.Current.Shutdown(0) — 退出应用。
    public class ExitApplicationCommand : IUICommand
    {
        public string Id => "ExitApplication";
        public string Header => ServiceLocator.Localization.Translate("Exit", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🚪";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: Application.Current.Shutdown(0) / Avalonia Application.Current.Shutdown()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
