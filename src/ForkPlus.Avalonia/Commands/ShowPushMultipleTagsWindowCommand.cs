using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowPushMultipleTagsWindowCommand.cs
    // WPF: 弹出 PushMultipleTagsWindow 一次性 push 多个 tag 到远端。
    public class ShowPushMultipleTagsWindowCommand : IUICommand
    {
        public string Id => "ShowPushMultipleTagsWindow";
        public string Header => ServiceLocator.Localization.Translate("Push Multiple Tags...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🏷";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new PushMultipleTagsWindow(repo).ShowDialog() → git push --tags
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
