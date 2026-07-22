using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowSaveRevisionsAsPatchWindowCommand.cs
    // WPF: 弹出 SaveAsPatchWindow 把选中 revisions 的 diff 保存为 patch 文件（git format-patch）。
    public class ShowSaveRevisionsAsPatchWindowCommand : IUICommand
    {
        public string Id => "ShowSaveRevisionsAsPatchWindow";
        public string Header => ServiceLocator.Localization.Translate("Save Revisions as Patch...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "💾";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new SaveAsPatchWindow().ShowDialog() → git format-patch
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
