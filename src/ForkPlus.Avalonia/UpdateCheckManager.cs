using System;
using System.Threading;
// Avalonia spike 版 UpdateCheckManager（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/UpdateCheckManager.cs（114 行）：
//   - WPF: internal class UpdateCheckManager
//   - DispatcherTimer：启动后延迟 30 秒首次自检，之后每小时 tick
//   - Timer_Tick：首次 tick 后切换到周期间隔；若 UpdateChecker.ShouldAutoCheck() → CheckAsync()
//   - CheckNow()：instance.Dispatcher.Invoke(() => new UpdateCheckWindow().ShowDialog())
//   - CheckAsync()：Task.Run 检查 _checker.CheckLatestRelease()，
//     若有更新且未跳过 → ShowUpdateAvailable(info)
//   - ShowUpdateAvailable：instance.Dispatcher.Invoke(() => new UpdateAvailableWindow(info).ShowDialog())
//   - 依赖：DispatcherTimer / UpdateChecker / UpdateInfo / UpdateCheckWindow / UpdateAvailableWindow
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF DispatcherTimer → spike 用 System.Threading.Timer（最轻量）
//   2. WPF instance.Dispatcher.Invoke → spike 用占位（不弹窗）
//   3. WPF UpdateChecker / UpdateInfo → spike 跳过（无 UpdateChecker 类）
//   4. WPF UpdateCheckWindow / UpdateAvailableWindow → spike 跳过（无对应窗口）
//   5. WPF UpdateChecker.ShouldAutoCheck / MarkChecked / IsVersionSkipped → spike 跳过
//
// spike 简化（task spec 关键 API）：
//   - public void Start()（启动定时器）
//   - public void CheckNow()（spike 占位，只打日志）
namespace ForkPlus.Avalonia
{
    // spike 版：放在 ForkPlus.Avalonia 命名空间（task spec：Manager 类用此命名空间）。
    internal class UpdateCheckManager
    {
        // 对照 WPF: StartCheckInterval = 30 sec, RecurringCheckInterval = 1 hour
        // spike 版：保持间隔常量（不实际使用）
        private static readonly TimeSpan StartCheckInterval = TimeSpan.FromSeconds(30.0);
        private static readonly TimeSpan RecurringCheckInterval = TimeSpan.FromHours(1.0);

        // spike 版：用 System.Threading.Timer 替代 WPF DispatcherTimer
        private readonly Timer _timer;
        private bool _firstCheckDone;

        public UpdateCheckManager()
        {
            // 对照 WPF: _timer.Interval = StartCheckInterval; _timer.Tick += Timer_Tick;
            _timer = new Timer(OnTimerTick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        // 对照 WPF: public void Start() — _timer.Start();
        // spike 版：用 Timer.Change 启动
        public void Start()
        {
            _timer.Change(StartCheckInterval, RecurringCheckInterval);
        }

        // 对照 WPF: private void Timer_Tick(object sender, EventArgs e)
        // spike 版：tick 时只打日志，不实际检查更新
        private void OnTimerTick(object state)
        {
            if (!_firstCheckDone)
            {
                _firstCheckDone = true;
                _timer.Change(RecurringCheckInterval, RecurringCheckInterval);
            }
            // spike 版跳过：无 UpdateChecker.ShouldAutoCheck / CheckAsync 调用
            Console.WriteLine("[UpdateCheckManager] Tick (spike skipped)");
        }

        // 对照 WPF: public void CheckNow() — instance.Dispatcher.Invoke(() => new UpdateCheckWindow().ShowDialog());
        // spike 版：占位（不弹 UpdateCheckWindow）
        public void CheckNow()
        {
            Console.WriteLine("[UpdateCheckManager] CheckNow (spike skipped)");
        }
    }
}
