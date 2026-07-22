using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/OpenKeyboardShortcutsCommand.cs
    // WPF: 打开 KeyboardShortcutsWindow 显示所有快捷键。
    public class OpenKeyboardShortcutsCommand : IUICommand
    {
        public string Id => "OpenKeyboardShortcuts";
        public string Header => ServiceLocator.Localization.Translate("Keyboard Shortcuts", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⌨️";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new KeyboardShortcutsWindow().ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
