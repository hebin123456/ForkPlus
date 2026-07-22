using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowAskPassWindowCommand.cs
    // WPF: 弹出 AskPassWindow 让用户输入 HTTPS 凭据（git askpass 集成）。
    public class ShowAskPassWindowCommand : IUICommand
    {
        public string Id => "ShowAskPassWindow";
        public string Header => ServiceLocator.Localization.Translate("Enter Credentials", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔑";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new AskPassWindow().ShowDialog() → 返回用户名/密码
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
