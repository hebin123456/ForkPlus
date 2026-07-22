using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/OpenForkPlusTwitterCommand.cs
    // WPF: 在浏览器中打开 ForkPlus 的 Twitter 主页。
    public class OpenForkPlusTwitterCommand : IUICommand
    {
        public string Id => "OpenForkPlusTwitter";
        public string Header => ServiceLocator.Localization.Translate("Follow Us on Twitter", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🐦";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new Uri(twitterUrl).OpenInBrowser()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
