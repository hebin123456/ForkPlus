using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowPushMultipleBranchesWindowCommand.cs
    // WPF: 弹出 PushMultipleBranchesWindow 一次性 push 多个本地分支到远端。
    public class ShowPushMultipleBranchesWindowCommand : IUICommand
    {
        public string Id => "ShowPushMultipleBranchesWindow";
        public string Header => ServiceLocator.Localization.Translate("Push Multiple Branches...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⬆";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new PushMultipleBranchesWindow(repo).ShowDialog() → 多个 PushGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
