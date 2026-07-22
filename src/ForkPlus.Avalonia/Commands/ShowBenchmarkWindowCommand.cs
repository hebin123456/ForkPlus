using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowBenchmarkWindowCommand.cs
    // WPF: 打开 BenchmarkWindow 性能基准测试窗口（开发/调试用）。
    public class ShowBenchmarkWindowCommand : IUICommand
    {
        public string Id => "ShowBenchmarkWindow";
        public string Header => ServiceLocator.Localization.Translate("Benchmark", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⏱";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new BenchmarkWindow().ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
