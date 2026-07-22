using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/OpenForkPlusWebsiteCommand.cs
    // WPF: 在浏览器中打开 ForkPlus 官网。
    public class OpenForkPlusWebsiteCommand : IUICommand
    {
        public string Id => "OpenForkPlusWebsite";
        public string Header => ServiceLocator.Localization.Translate("Visit Website", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🌐";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new Uri(websiteUrl).OpenInBrowser()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
