using System;
using Avalonia.Threading;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Services
{
    /// <summary>
    /// ITimerService 的 Avalonia 实现，封装 Avalonia.Threading.DispatcherTimer。
    ///
    /// 对照 WPF 实现 src/ForkPlus/Services/Wpf/WpfTimerService.cs（封装 System.Windows.Threading.DispatcherTimer）。
    /// Avalonia 的 DispatcherTimer API 与 WPF 几乎对称：
    ///   - Interval 属性（TimeSpan）
    ///   - IsEnabled 属性
    ///   - Tick 事件（EventHandler）
    ///   - Start() / Stop() 方法
    /// Tick 在 UI 线程触发，与 WPF 行为一致。
    ///
    /// 调用方：ForkPlus.Core.Accounts.NotificationManager（仅 1 处，通知轮询定时器）。
    /// </summary>
    public class AvaloniaTimerService : ITimerService
    {
        private readonly DispatcherTimer _timer;

        /// <summary>Timer 触发间隔。等价 DispatcherTimer.Interval。</summary>
        public TimeSpan Interval
        {
            get => _timer.Interval;
            set => _timer.Interval = value;
        }

        /// <summary>Timer 是否正在运行。等价 DispatcherTimer.IsEnabled。</summary>
        public bool IsEnabled => _timer.IsEnabled;

        /// <summary>Timer 触发事件。订阅时转发 DispatcherTimer.Tick。</summary>
        public event EventHandler Tick;

        /// <summary>默认构造。创建 DispatcherTimer 并订阅 Tick 事件转发。</summary>
        public AvaloniaTimerService()
        {
            _timer = new DispatcherTimer();
            _timer.Tick += OnTimerTick;
        }

        /// <summary>带初始间隔的构造。等价 WpfTimerService(TimeSpan interval)。</summary>
        public AvaloniaTimerService(TimeSpan interval) : this()
        {
            _timer.Interval = interval;
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            Tick?.Invoke(this, e);
        }

        /// <summary>启动 Timer。等价 DispatcherTimer.Start()。</summary>
        public void Start() => _timer.Start();

        /// <summary>停止 Timer。等价 DispatcherTimer.Stop()。</summary>
        public void Stop() => _timer.Stop();

        /// <summary>释放资源。停止 Timer 并取消 Tick 事件订阅，避免内存泄漏。</summary>
        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= OnTimerTick;
        }
    }
}
