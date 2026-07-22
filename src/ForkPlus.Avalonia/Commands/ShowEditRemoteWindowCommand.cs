using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowEditRemoteWindowCommand.cs
    // WPF: 弹出 EditRemoteWindow 编辑远端仓库的 URL / 名称。
    public class ShowEditRemoteWindowCommand : IUICommand
    {
        public string Id => "ShowEditRemoteWindow";
        public string Header => ServiceLocator.Localization.Translate("Edit Remote...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "✏️";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new EditRemoteWindow(repo, remote).ShowDialog() → SetRemoteUrlGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
