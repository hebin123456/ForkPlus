using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/CopyAbsoluteFilePathsCommand.cs
    // WPF: 把选中文件的绝对路径复制到剪贴板（Ctrl+Shift+C）。
    public class CopyAbsoluteFilePathsCommand : IUICommand
    {
        public string Id => "CopyAbsoluteFilePaths";
        public string Header => ServiceLocator.Localization.Translate("Copy Full Path", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📋";
        public string ShortcutText => "Ctrl+Shift+C";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: ServiceLocator.Clipboard.SetText(Path.Combine(gitModulePath, filePaths))
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
