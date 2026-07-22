using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowQuickLaunchWindowCommand.cs
    // WPF: 弹出 QuickLaunchWindow 命令面板（Quick Launch）。Ctrl+Shift+P（与 Push 冲突时由 palette 处理）。
    public class ShowQuickLaunchWindowCommand : IUICommand
    {
        public string Id => "ShowQuickLaunchWindow";
        public string Header => ServiceLocator.Localization.Translate("Quick Launch", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⚡";
        public string ShortcutText => "Ctrl+Shift+P";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new QuickLaunchWindow().ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
