using System;
// Avalonia spike 版 AutomaticBackgroundFetchManager（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/AutomaticBackgroundFetchManager.cs（113 行）：
//   - WPF: internal class AutomaticBackgroundFetchManager
//   - DispatcherTimer：启动 1 分钟首次 tick，之后每 10 分钟 tick
//   - _dispatcherTimer_Tick：若 MainWindow 非活动 → 延迟到下个 1 分钟
//     若 FetchRemotesAutomatically → 遍历 TabManager.RepositoryUserControls，
//     对每个 GitModule 调 GetRemotesGitCommand + FetchGitCommand
//   - 依赖：DispatcherTimer / MainWindow.Instance / MainWindow.Instance.TabManager.RepositoryUserControls /
//     MainWindow.Instance.JobQueue / GitModule / GetRemotesGitCommand / FetchGitCommand /
//     PreferencesLocalization.Current / SubDomain
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF DispatcherTimer → Avalonia 也用 Avalonia.Threading.DispatcherTimer（API 一致）
//      spike 版简化：用 System.Threading.Timer（最轻量，不依赖 Avalonia.Threading）
//   2. WPF MainWindow.Instance.IsActive → spike 跳过活动检测
//   3. WPF MainWindow.Instance.TabManager.RepositoryUserControls → spike 跳过
//      （MainWindow.TabManager 是 ClosableTabControl，无 RepositoryUserControls 属性）
//   4. WPF MainWindow.Instance.JobQueue.Add → spike 跳过（JobQueue 为 null 占位）
//   5. WPF GetRemotesGitCommand / FetchGitCommand → spike 跳过（无 Git 依赖）
//   6. WPF PreferencesLocalization.Current → spike 跳过（无 Localization 依赖）
//
// spike 简化（task spec 关键 API）：
//   - 构造函数启动定时器（spike 占位，tick 时只打日志不实际 fetch）
namespace ForkPlus.Avalonia
{
    // spike 版：放在 ForkPlus.Avalonia 命名空间（task spec：Manager 类用此命名空间）。
    internal class AutomaticBackgroundFetchManager
    {
        // 对照 WPF: StartFetchInterval = 1 min, RecurringFetchInterval = 10 min
        // spike 版：保持间隔常量（不实际使用）
        private static readonly TimeSpan StartFetchInterval = TimeSpan.FromMinutes(1.0);
        private static readonly TimeSpan RecurringFetchInterval = TimeSpan.FromMinutes(10.0);

        // spike 版：用 System.Threading.Timer 替代 WPF DispatcherTimer
        private readonly System.Threading.Timer _timer;

        public AutomaticBackgroundFetchManager()
        {
            // 对照 WPF: _dispatcherTimer.Interval = StartFetchInterval; _dispatcherTimer.Tick += ...; _dispatcherTimer.Start();
            // spike 版：用 Timer 1 分钟首次触发，之后 10 分钟周期
            _timer = new System.Threading.Timer(OnTimerTick, null,
                StartFetchInterval, RecurringFetchInterval);
        }

        // 对照 WPF: private void _dispatcherTimer_Tick(object sender, EventArgs e)
        // spike 版：tick 时只打日志，不实际 fetch
        private void OnTimerTick(object state)
        {
            // spike 版跳过：无 GitModule 遍历 + FetchGitCommand 调用
            Console.WriteLine("[AutomaticBackgroundFetchManager] Tick (spike skipped)");
        }
    }
}
