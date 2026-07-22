using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/DiscardChangedFilesCommand.cs（199 行）
    // WPF: 确认后 DiscardFileChangesGitCommand.Execute → RefreshFileStatusCommand。
    // spike: 核心逻辑在 DiscardFileChangesGitCommand + CommitUserControl，命令仅暴露 Execute 签名。
    public class DiscardChangedFilesCommand : IUICommand
    {
        public string Id => "DiscardChangedFiles";
        public string Header => ServiceLocator.Localization.Translate("Discard changes...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🗑";
        public string ShortcutText => "Ctrl+Shift+D";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: MessageBoxWindow 确认 → DiscardFileChangesGitCommand.Execute → RefreshFileStatusCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
