using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowRepositoryOverviewWindowCommand.cs
    // WPF: 打开 RepositoryOverviewWindow 显示仓库总览（贡献热力图 / 最近活动 / 统计）。
    public class ShowRepositoryOverviewWindowCommand : IUICommand
    {
        public string Id => "ShowRepositoryOverviewWindow";
        public string Header => ServiceLocator.Localization.Translate("Repository Overview", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📊";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new RepositoryOverviewWindow(repo).ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
