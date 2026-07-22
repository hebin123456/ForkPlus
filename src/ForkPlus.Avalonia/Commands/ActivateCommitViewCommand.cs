using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ActivateCommitViewCommand.cs
    // WPF: MainWindow.ActiveRepositoryUserControl?.ActivateCommitView() — 切换到 Commit 视图（Ctrl+1）。
    public class ActivateCommitViewCommand : IUICommand
    {
        public string Id => "ActivateCommitView";
        public string Header => ServiceLocator.Localization.Translate("Show Uncommitted Changes", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📝";
        public string ShortcutText => "Ctrl+1";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: MainWindow.ActiveRepositoryUserControl?.ActivateCommitView()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
