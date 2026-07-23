using System;
using Avalonia.Threading;

namespace ForkPlus.UI
{
	public static class DispatcherExtension
	{
		/// <summary>Avalonia 无 DispatcherOperation 等价物，Post 是 fire-and-forget。
		/// 调用方若需要等待完成，应直接用 Dispatcher.InvokeAsync。</summary>
		public static void Async(this Dispatcher dispatcher, Action action)
		{
			dispatcher.Post(action);
		}

		public static void Sync(this Dispatcher dispatcher, Action action)
		{
			dispatcher.Invoke(action);
		}
	}
}
