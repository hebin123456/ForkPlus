using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowAddRemoteWindowCommand.cs
    // WPF: 弹出 AddRemoteWindow 添加远端仓库（remote）。
    public class ShowAddRemoteWindowCommand : IUICommand
    {
        public string Id => "ShowAddRemoteWindow";
        public string Header => ServiceLocator.Localization.Translate("Add Remote...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "➕";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new AddRemoteWindow(repo).ShowDialog() → AddRemoteGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
