using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/UpdateTrackingReferenceCommand.cs
    // WPF: 修改本地分支的上游追踪关系（git branch --set-upstream-to）。
    public class UpdateTrackingReferenceCommand : IUICommand
    {
        public string Id => "UpdateTrackingReference";
        public string Header => ServiceLocator.Localization.Translate("Update Tracking Reference", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔗";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: SetUpstreamGitCommand → repo.InvalidateAndRefresh(References)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
