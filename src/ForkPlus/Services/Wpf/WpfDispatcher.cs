using System;
using System.Windows.Threading;
using Avalonia.Threading;

namespace ForkPlus.Services.Wpf
{
	public class WpfDispatcher : IDispatcher
	{
		private readonly Dispatcher _dispatcher;

		public WpfDispatcher(Dispatcher dispatcher)
		{
			_dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
		}

		public void Post(Action action)
		{
			_dispatcher.BeginInvoke(action);
		}

		public void Invoke(Action action)
		{
			_dispatcher.Invoke(action);
		}
	}
}
