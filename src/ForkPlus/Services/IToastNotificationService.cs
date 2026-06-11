using System;

namespace ForkPlus.Services
{
	/// <summary>
	/// 平台无关的 Toast 通知服务接口。
	/// WPF 实现使用 WinRT ToastNotifications，Avalonia 实现使用各平台原生通知。
	/// </summary>
	public interface IToastNotificationService
	{
		void Show(string xmlPayload);
	}
}
