using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowPerformanceDiagnosticsWindowCommand.cs
    // WPF: 打开 PerformanceDiagnosticsWindow 显示性能诊断信息（开发/调试用）。
    public class ShowPerformanceDiagnosticsWindowCommand : IUICommand
    {
        public string Id => "ShowPerformanceDiagnosticsWindow";
        public string Header => ServiceLocator.Localization.Translate("Performance Diagnostics", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📊";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new PerformanceDiagnosticsWindow().ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
