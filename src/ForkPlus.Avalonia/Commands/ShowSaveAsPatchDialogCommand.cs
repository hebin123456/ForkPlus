using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowSaveAsPatchDialogCommand.cs
    // WPF: 弹出 SaveAsPatchWindow 把当前 diff 保存为 patch 文件。
    public class ShowSaveAsPatchDialogCommand : IUICommand
    {
        public string Id => "ShowSaveAsPatchDialog";
        public string Header => ServiceLocator.Localization.Translate("Save as Patch...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "💾";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new SaveAsPatchWindow().ShowDialog() → 写入 .patch 文件
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
