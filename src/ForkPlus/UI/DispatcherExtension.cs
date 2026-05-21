using System;
using System.Windows.Threading;

namespace ForkPlus.UI
{
	public static class DispatcherExtension
	{
		public static DispatcherOperation Async(this Dispatcher dispatcher, Action action)
		{
			return dispatcher.BeginInvoke(action);
		}

		public static void Sync(this Dispatcher dispatcher, Action action)
		{
			dispatcher.Invoke(action);
		}
	}
}
