using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/OpenRepositoryInFileExplorerCommand.cs
    // WPF: 在系统文件管理器中打开当前仓库根目录。
    public class OpenRepositoryInFileExplorerCommand : IUICommand
    {
        public string Id => "OpenRepositoryInFileExplorer";
        public string Header => ServiceLocator.Localization.Translate("Reveal in File Manager", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📁";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 用系统文件管理器打开 repo.GitModule.WorkingDir
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
