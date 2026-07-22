using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowFileInFileExplorerCommand.cs
    // WPF: 在系统文件管理器中定位到选中的文件。
    public class ShowFileInFileExplorerCommand : IUICommand
    {
        public string Id => "ShowFileInFileExplorer";
        public string Header => ServiceLocator.Localization.Translate("Reveal in File Manager", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📁";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 用系统文件管理器定位到 repo 中的选中文件
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
