using System;

namespace ForkPlus.Services
{
	/// <summary>
	/// 平台无关的定时器接口。
	/// WPF 实现使用 <c>DispatcherTimer</c>，Avalonia 实现使用 <c>DispatcherTimer</c> 或 System.Timers.Timer。
	/// </summary>
	public interface ITimerService : IDisposable
	{
		TimeSpan Interval { get; set; }
		bool IsEnabled { get; }
		event EventHandler Tick;
		void Start();
		void Stop();
	}
}
