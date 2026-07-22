using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowCreateTagWindowCommand.cs
    // WPF: 弹出 CreateTagWindow 在指定 revision 上创建新 tag（git tag）。
    public class ShowCreateTagWindowCommand : IUICommand
    {
        public string Id => "ShowCreateTagWindow";
        public string Header => ServiceLocator.Localization.Translate("New Tag...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🏷";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new CreateTagWindow(repo, revision).ShowDialog() → CreateTagGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
