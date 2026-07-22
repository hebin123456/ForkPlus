using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowDebugUpdateWindowCommand.cs
    // WPF: 打开 DebugUpdateWindow 调试应用更新流程（开发用）。
    public class ShowDebugUpdateWindowCommand : IUICommand
    {
        public string Id => "ShowDebugUpdateWindow";
        public string Header => ServiceLocator.Localization.Translate("Debug Update", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🐞";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new UpdateAvailableWindow().ShowDialog()（调试模式）
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
