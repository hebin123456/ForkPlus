using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowSideBySideMergeWindowCommand.cs
    // WPF: 弹出 SideBySideMergeWindow 用并排方式解决冲突（三方合并视图）。
    public class ShowSideBySideMergeWindowCommand : IUICommand
    {
        public string Id => "ShowSideBySideMergeWindow";
        public string Header => ServiceLocator.Localization.Translate("Merge Side by Side", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⚖️";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new SideBySideMergeWindow(repo, file).ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
