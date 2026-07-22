using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowPushTagWindowCommand.cs
    // WPF: 弹出 PushTagWindow push 指定 tag 到远端（git push <remote> <tag>）。
    public class ShowPushTagWindowCommand : IUICommand
    {
        public string Id => "ShowPushTagWindow";
        public string Header => ServiceLocator.Localization.Translate("Push Tag...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🏷";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new PushTagWindow(repo, tag).ShowDialog() → PushTagGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
