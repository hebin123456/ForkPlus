using System;
using System.Windows.Threading;
using ForkPlus.Services;

namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// WPF 平台的 TimerService，封装 <c>DispatcherTimer</c>。
	/// 在 UI 线程上触发 Tick，适合需要访问 WPF 控件的场景。
	/// </summary>
	public class WpfTimerService : ITimerService
	{
		private readonly DispatcherTimer _timer;

		public TimeSpan Interval
		{
			get => _timer.Interval;
			set => _timer.Interval = value;
		}

		public bool IsEnabled => _timer.IsEnabled;

		public event EventHandler Tick;

		public WpfTimerService()
		{
			_timer = new DispatcherTimer();
			_timer.Tick += _timer_Tick;
		}

		public WpfTimerService(TimeSpan interval) : this()
		{
			_timer.Interval = interval;
		}

		private void _timer_Tick(object sender, EventArgs e)
		{
			Tick?.Invoke(this, e);
		}

		public void Start() => _timer.Start();

		public void Stop() => _timer.Stop();

		public void Dispose()
		{
			_timer.Stop();
			_timer.Tick -= _timer_Tick;
		}
	}
}
