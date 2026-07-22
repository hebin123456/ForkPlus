using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/QuickPushCommand.cs
    // WPF: 快速 push（用默认 remote + 当前分支，无对话框）。
    public class QuickPushCommand : IUICommand
    {
        public string Id => "QuickPush";
        public string Header => ServiceLocator.Localization.Translate("Quick Push", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⬆";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 取默认 remote + 当前分支 → JobQueue.Add(PushGitCommand) → InvalidateAndRefresh
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
