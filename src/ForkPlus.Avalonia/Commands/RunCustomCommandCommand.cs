using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/RunCustomCommandCommand.cs
    // WPF: 运行用户自定义的 git 命令（Custom Commands 配置）。
    public class RunCustomCommandCommand : IUICommand
    {
        public string Id => "RunCustomCommand";
        public string Header => ServiceLocator.Localization.Translate("Run Custom Command", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "▶️";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 在 JobQueue 中执行用户配置的 shell 命令
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
