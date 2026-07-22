using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowRemoveRemoteWindowCommand.cs
    // WPF: 弹出 RemoveRemoteWindow 删除远端仓库（git remote remove）。
    public class ShowRemoveRemoteWindowCommand : IUICommand
    {
        public string Id => "ShowRemoveRemoteWindow";
        public string Header => ServiceLocator.Localization.Translate("Remove Remote...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🗑";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new RemoveRemoteWindow(repo, remote).ShowDialog() → RemoveRemoteGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
