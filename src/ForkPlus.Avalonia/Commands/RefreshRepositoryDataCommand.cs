using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/RefreshRepositoryDataCommand.cs
    // WPF: 仅刷新仓库数据层（revisions + references），不刷新 status。
    public class RefreshRepositoryDataCommand : IUICommand
    {
        public string Id => "RefreshRepositoryData";
        public string Header => ServiceLocator.Localization.Translate("Refresh Repository Data", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔄";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: repo.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.References)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
