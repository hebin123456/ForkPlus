using System;
using System.Timers;
using ForkPlus.Services;

namespace ForkPlus
{
	/// <summary>
	/// 带有延迟执行功能的泛型动作，使用 System.Timers.Timer 实现线程池计时。
	/// 默认在 ThreadPool 线程上执行回调 —— 若需要回到 UI 线程，请在 action 内部使用 ServiceLocator.Dispatcher。
	/// </summary>
	public class DelayedAction<T>
	{
		private Timer _timer;

		private Action<T> _action;

		private T _parameter;

		private readonly object _lock = new object();

		public DelayedAction(Action<T> action, double delaySeconds = 0.01)
		{
			_timer = new Timer(delaySeconds * 1000.0);
			_timer.AutoReset = false;
			_timer.Elapsed += Timer_Elapsed;
			_action = action;
		}

		public void InvokeWithDelay(T parameter)
		{
			lock (_lock)
			{
				_timer.Stop();
				_parameter = parameter;
				_timer.Start();
			}
		}

		public void InvokeNow(T parameter)
		{
			lock (_lock)
			{
				_timer.Stop();
				_parameter = parameter;
				RunAction();
			}
		}

		public void ReinvokeNow()
		{
			lock (_lock)
			{
				_timer.Stop();
				RunAction();
			}
		}

		public void Cancel()
		{
			lock (_lock)
			{
				_timer.Stop();
			}
		}

		private void Timer_Elapsed(object sender, ElapsedEventArgs e)
		{
		 lock (_lock)
		 {
		  ServiceLocator.Dispatcher?.Post(() => RunAction());
		 }
		}

		private void RunAction()
		{
		 _timer.Stop();
		 _action(_parameter);
		}

		public void Dispose()
		{
			lock (_lock)
			{
				_timer?.Stop();
				_timer?.Dispose();
				_timer = null;
			}
		}
	}
}
