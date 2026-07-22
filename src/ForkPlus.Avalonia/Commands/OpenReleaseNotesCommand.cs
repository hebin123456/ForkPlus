using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/OpenReleaseNotesCommand.cs
    // WPF: 在浏览器中打开 ForkPlus 的 Release Notes 页面。
    public class OpenReleaseNotesCommand : IUICommand
    {
        public string Id => "OpenReleaseNotes";
        public string Header => ServiceLocator.Localization.Translate("Release Notes", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📋";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new Uri(releaseNotesUrl).OpenInBrowser()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
