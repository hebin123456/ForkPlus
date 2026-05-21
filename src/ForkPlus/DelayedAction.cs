using System;
using System.Windows.Threading;

namespace ForkPlus
{
	public class DelayedAction<T>
	{
		private DispatcherTimer _timer;

		private Action<T> _action;

		private T _parameter;

		public DelayedAction(Action<T> action, double delaySeconds = 0.01)
		{
			_timer = new DispatcherTimer();
			_timer.Interval = TimeSpan.FromSeconds(delaySeconds);
			_timer.Tick += Timer_Tick;
			_action = action;
		}

		public void InvokeWithDelay(T parameter)
		{
			_timer.Stop();
			_parameter = parameter;
			_timer.Start();
		}

		public void InvokeNow(T parameter)
		{
			_timer.Stop();
			_parameter = parameter;
			RunAction();
		}

		public void ReinvokeNow()
		{
			_timer.Stop();
			RunAction();
		}

		public void Cancel()
		{
			_timer.Stop();
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			_timer.Stop();
			RunAction();
		}

		private void RunAction()
		{
			_action(_parameter);
		}
	}
}
