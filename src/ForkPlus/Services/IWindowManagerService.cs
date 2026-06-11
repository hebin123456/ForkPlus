using System;

namespace ForkPlus.Services
{
	/// <summary>
	/// 平台无关的窗口管理接口（查找/激活窗口）。
	/// WPF 实现使用 <c>Application.Current.Windows</c>，Avalonia 实现使用 <c>Application.Current.Windows</c>。
	/// </summary>
	public interface IWindowManagerService
	{
		void ActivateAndShowNotifications();
		bool TryActivateWindowByTitle(string title);
		void DispatchToUiThread(Action action);
	}
}
