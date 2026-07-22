using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowConfigureSshKeysCommand.cs
    // WPF: 打开 ConfigureSshKeysWindow 管理 SSH 密钥（导入 / 生成 / 删除）。
    public class ShowConfigureSshKeysCommand : IUICommand
    {
        public string Id => "ShowConfigureSshKeys";
        public string Header => ServiceLocator.Localization.Translate("SSH Keys...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔑";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new ConfigureSshKeysWindow().ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
