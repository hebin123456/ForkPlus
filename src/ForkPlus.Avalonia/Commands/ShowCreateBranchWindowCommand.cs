using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowCreateBranchWindowCommand.cs
    // WPF: 弹出 CreateBranchWindow 在指定 revision 上创建新分支（git branch）。
    public class ShowCreateBranchWindowCommand : IUICommand
    {
        public string Id => "ShowCreateBranchWindow";
        public string Header => ServiceLocator.Localization.Translate("New Branch...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🌿";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new CreateBranchWindow(repo, revision).ShowDialog() → CreateBranchGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
