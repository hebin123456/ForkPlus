using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/RefreshFileStatusCommand.cs
    // WPF: 刷新当前仓库的文件状态（refresh git status）。
    public class RefreshFileStatusCommand : IUICommand
    {
        public string Id => "RefreshFileStatus";
        public string Header => ServiceLocator.Localization.Translate("Refresh File Status", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔄";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: RefreshFileStatusGitCommand → repo.UpdateRepositoryStatus
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
