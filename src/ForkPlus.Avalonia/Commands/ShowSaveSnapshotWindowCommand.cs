using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowSaveSnapshotWindowCommand.cs
    // WPF: 弹出 SaveSnapshotWindow 保存当前仓库状态快照（ForkPlus 自有快照功能）。
    public class ShowSaveSnapshotWindowCommand : IUICommand
    {
        public string Id => "ShowSaveSnapshotWindow";
        public string Header => ServiceLocator.Localization.Translate("Save Snapshot...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📸";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new SaveSnapshotWindow(repo).ShowDialog() → 保存 HEAD + 工作区快照
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
