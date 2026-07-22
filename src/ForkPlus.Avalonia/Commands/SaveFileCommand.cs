using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/SaveFileCommand.cs
    // WPF: 保存当前编辑中的文件（Diff 编辑器 / 文本编辑器）。
    public class SaveFileCommand : IUICommand
    {
        public string Id => "SaveFile";
        public string Header => ServiceLocator.Localization.Translate("Save", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "💾";
        public string ShortcutText => "Ctrl+S";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 保存当前编辑器中的文件内容到磁盘
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
