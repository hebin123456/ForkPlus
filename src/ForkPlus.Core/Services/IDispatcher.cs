using System;

namespace ForkPlus.Services
{
	/// <summary>
	/// 跨平台 UI 线程调度抽象（替换 WPF Dispatcher）
	/// </summary>
	public interface IDispatcher
	{
		void Post(Action action);
		void Invoke(Action action);
	}
}
