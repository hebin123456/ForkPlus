using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowRepositoryStatisticsWindowCommand.cs
    // WPF: 打开 RepositoryStatisticsWindow 显示仓库统计（提交数 / 行数 / 作者分布等图表）。
    public class ShowRepositoryStatisticsWindowCommand : IUICommand
    {
        public string Id => "ShowRepositoryStatisticsWindow";
        public string Header => ServiceLocator.Localization.Translate("Statistics", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📊";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new RepositoryStatisticsWindow(repo).ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
