using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowCloneWindowCommand.cs
    // WPF: 弹出 CloneWindow 克隆远端仓库（git clone）。
    public class ShowCloneWindowCommand : IUICommand
    {
        public string Id => "ShowCloneWindow";
        public string Header => ServiceLocator.Localization.Translate("Clone...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⬇";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new CloneWindow().ShowDialog() → CloneGitCommand → TabManager.OpenRepository
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
