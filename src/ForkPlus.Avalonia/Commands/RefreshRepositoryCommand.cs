using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/RefreshRepositoryCommand.cs
    // WPF: 刷新整个仓库（revisions + references + status + remotes）。
    public class RefreshRepositoryCommand : IUICommand
    {
        public string Id => "RefreshRepository";
        public string Header => ServiceLocator.Localization.Translate("Refresh", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔄";
        public string ShortcutText => "F5";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: repo.InvalidateAndRefresh(SubDomain.All)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
