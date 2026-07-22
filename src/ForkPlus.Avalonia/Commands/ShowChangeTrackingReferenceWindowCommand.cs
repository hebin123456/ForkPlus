using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowChangeTrackingReferenceWindowCommand.cs
    // WPF: 弹出 ChangeRemoteTrackingWindow 修改本地分支的上游追踪关系。
    public class ShowChangeTrackingReferenceWindowCommand : IUICommand
    {
        public string Id => "ShowChangeTrackingReferenceWindow";
        public string Header => ServiceLocator.Localization.Translate("Change Tracking Reference...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔗";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new ChangeRemoteTrackingWindow(repo, branch).ShowDialog() → SetUpstreamGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
