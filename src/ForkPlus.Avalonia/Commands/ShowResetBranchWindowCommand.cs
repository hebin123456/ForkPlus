using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowResetBranchWindowCommand.cs
    // WPF: 弹出 ResetBranchWindow 把当前分支 reset 到指定 revision（git reset --soft/--mixed/--hard）。
    public class ShowResetBranchWindowCommand : IUICommand
    {
        public string Id => "ShowResetBranchWindow";
        public string Header => ServiceLocator.Localization.Translate("Reset to this Revision...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "↩";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new ResetBranchWindow(repo, revision).ShowDialog() → ResetGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
