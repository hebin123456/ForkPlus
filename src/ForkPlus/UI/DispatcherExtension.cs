using System;
using Avalonia.Threading;

namespace ForkPlus.UI
{
	public static class DispatcherExtension
	{
		public static DispatcherOperation Async(this Dispatcher dispatcher, Action action)
		{
			return dispatcher.InvokeAsync(action);
		}

		public static void Sync(this Dispatcher dispatcher, Action action)
		{
			dispatcher.Invoke(action);
		}
	}
}
