using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowAboutWindowCommand.cs
    // WPF: 打开关于窗口（"About ForkPlus"）。
    public class ShowAboutWindowCommand : IUICommand
    {
        public string Id => "ShowAboutWindow";
        public string Header => ServiceLocator.Localization.Translate("About ForkPlus", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "ℹ️";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new AboutWindow().ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
