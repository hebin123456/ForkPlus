using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowRenameStashWindowCommand.cs
    // WPF: 弹出 RenameStashWindow 重命名 stash（git stash rename，需要 ForkPlus 内部映射）。
    public class ShowRenameStashWindowCommand : IUICommand
    {
        public string Id => "ShowRenameStashWindow";
        public string Header => ServiceLocator.Localization.Translate("Rename Stash...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "✏️";
        public string ShortcutText => "F2";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new RenameStashWindow(repo, stash).ShowDialog() → 更新 stash message 映射
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
