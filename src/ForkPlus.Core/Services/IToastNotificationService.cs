using System;

namespace ForkPlus.Services
{
	/// <summary>
	/// 平台无关的 Toast 通知服务接口。
	/// WPF 实现使用 WinRT ToastNotifications，Avalonia 实现使用各平台原生通知。
	/// </summary>
	public interface IToastNotificationService
	{
		/// <summary>显示一个 Toast 通知（payload 为平台特定 XML/JSON 格式）。</summary>
		void Show(string xmlPayload);

		/// <summary>用户点击 Toast 通知时触发，参数为通知的 argument 字符串。
		/// Phase 0.5：NotificationManager 通过此事件接收用户点击回调，
		/// 替代原 CommunityToolkit.WinUI.Notifications.ToastNotificationManagerCompat.OnActivated。</summary>
		event Action<string> OnActivated;
	}
}
