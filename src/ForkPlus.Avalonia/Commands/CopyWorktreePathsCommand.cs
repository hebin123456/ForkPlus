using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/CopyWorktreePathsCommand.cs
    // WPF: ServiceLocator.Clipboard.SetText(worktree.Path) — 复制 worktree 路径到剪贴板。
    public class CopyWorktreePathsCommand : IUICommand
    {
        public string Id => "CopyWorktreePaths";
        public string Header => ServiceLocator.Localization.Translate("Copy Path", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📋";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: ServiceLocator.Clipboard.SetText(string.Join(NewLine, worktree.Path))
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
