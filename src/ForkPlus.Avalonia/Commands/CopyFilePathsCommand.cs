using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/CopyFilePathsCommand.cs
    // WPF: 把选中文件的相对路径复制到剪贴板（Ctrl+C）。
    public class CopyFilePathsCommand : IUICommand
    {
        public string Id => "CopyFilePaths";
        public string Header => ServiceLocator.Localization.Translate("Copy Path", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📋";
        public string ShortcutText => "Ctrl+C";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: ServiceLocator.Clipboard.SetText(string.Join(NewLine, filePaths))
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
