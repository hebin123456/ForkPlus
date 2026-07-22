using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/DiscardChunkCommand.cs
    // WPF: 确认后 ApplyWorkingTreeGitCommand.Execute 丢弃选中 diff chunk。
    public class DiscardChunkCommand : IUICommand
    {
        public string Id => "DiscardChunk";
        public string Header => ServiceLocator.Localization.Translate("Discard...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🗑";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: MessageBoxWindow 确认 → ApplyWorkingTreeGitCommand.Execute(patchData) → RefreshFileStatusCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
