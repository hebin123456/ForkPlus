using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/SendCrashReportCommand.cs
    // WPF: 上传崩溃日志到 ForkPlus 服务器（崩溃后自动调用）。
    public class SendCrashReportCommand : IUICommand
    {
        public string Id => "SendCrashReport";
        public string Header => ServiceLocator.Localization.Translate("Send Crash Report", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📤";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 上传崩溃日志到服务器
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
