using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ToggleTraceElapsedTimeCommand.cs
    // WPF: 切换 "在日志中记录每步耗时" 调试开关。
    public class ToggleTraceElapsedTimeCommand : IUICommand
    {
        public string Id => "ToggleTraceElapsedTime";
        public string Header => ServiceLocator.Localization.Translate("Trace Elapsed Time", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⏱";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: ForkPlusSettings.Default.TraceElapsedTime toggle
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
