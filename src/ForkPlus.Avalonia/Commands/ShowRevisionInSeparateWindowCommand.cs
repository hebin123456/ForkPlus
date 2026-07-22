using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowRevisionInSeparateWindowCommand.cs
    // WPF: 在独立窗口中打开选中 revision 的详情（用于跨仓库对比 / 多窗口浏览）。
    public class ShowRevisionInSeparateWindowCommand : IUICommand
    {
        public string Id => "ShowRevisionInSeparateWindow";
        public string Header => ServiceLocator.Localization.Translate("Open in Separate Window", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🪟";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new RevisionDetailsWindow(repo, revision).Show()
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
