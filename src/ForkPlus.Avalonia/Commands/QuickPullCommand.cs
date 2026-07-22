using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/QuickPullCommand.cs
    // WPF: 快速 pull（用默认 remote + 当前分支，无对话框）。
    public class QuickPullCommand : IUICommand
    {
        public string Id => "QuickPull";
        public string Header => ServiceLocator.Localization.Translate("Quick Pull", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⬇";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 取默认 remote + 当前分支 → JobQueue.Add(PullGitCommand) → InvalidateAndRefresh
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
