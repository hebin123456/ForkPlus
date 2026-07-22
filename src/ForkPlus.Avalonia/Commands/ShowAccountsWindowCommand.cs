using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowAccountsWindowCommand.cs
    // WPF: 打开 Accounts 窗口管理 GitHub / GitLab / Bitbucket 等账号。
    public class ShowAccountsWindowCommand : IUICommand
    {
        public string Id => "ShowAccountsWindow";
        public string Header => ServiceLocator.Localization.Translate("Accounts...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "👤";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new AccountsWindow().ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
