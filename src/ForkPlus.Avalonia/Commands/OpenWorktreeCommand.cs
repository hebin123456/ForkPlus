using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/OpenWorktreeCommand.cs
    // WPF: 打开选中的 worktree 作为独立仓库。
    public class OpenWorktreeCommand : IUICommand
    {
        public string Id => "OpenWorktree";
        public string Header => ServiceLocator.Localization.Translate("Open Worktree", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🌳";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 调用 TabManager.OpenRepository(worktreePath)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
