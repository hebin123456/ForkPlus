using System;

namespace ForkPlus.UI
{
	public class EventArgs<T> : EventArgs
	{
		public T Value { get; }

		public EventArgs(T value)
		{
			Value = value;
		}
	}
}
