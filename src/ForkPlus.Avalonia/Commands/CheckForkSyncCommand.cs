using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/CheckForkSyncCommand.cs
    // WPF: 弹出 ForkSyncCheckWindow 检测 fork 与 upstream 的同步状态（CheckForkSyncStatusGitCommand）。
    public class CheckForkSyncCommand : IUICommand
    {
        public string Id => "CheckForkSync";
        public string Header => ServiceLocator.Localization.Translate("Check Remote Sync...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔄";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new ForkSyncCheckWindow(repo, upstream, branch, branchName, null).ShowDialog()
            //        + JobQueue.Add(CheckForkSyncStatusGitCommand.Execute)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
