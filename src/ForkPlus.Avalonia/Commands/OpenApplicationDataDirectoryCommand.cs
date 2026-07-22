using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/OpenApplicationDataDirectoryCommand.cs
    // WPF: 在系统文件管理器中打开 ForkPlus 应用数据目录。
    public class OpenApplicationDataDirectoryCommand : IUICommand
    {
        public string Id => "OpenApplicationDataDirectory";
        public string Header => ServiceLocator.Localization.Translate("Open Application Data Directory", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📁";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 用系统文件管理器打开 AppData 目录
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
