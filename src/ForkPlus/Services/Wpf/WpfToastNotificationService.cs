using System;

namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// WPF/Windows 平台的 Toast 通知服务，封装 WinRT Toast API。
	/// 阶段 6：net10.0 目标下 CommunityToolkit / WinRT 不可用，整个 Show 体用 #if WINDOWS 守卫；
	/// 非 Windows 平台 Show 仅记日志（Toast 通知 Windows-only）。
	/// </summary>
	public class WpfToastNotificationService : IToastNotificationService
	{
		public void Show(string xmlPayload)
		{
#if WINDOWS
			try
			{
				Windows.Data.Xml.Dom.XmlDocument document = new Windows.Data.Xml.Dom.XmlDocument();
				document.LoadXml(xmlPayload);
				Windows.UI.Notifications.ToastNotifier notifier = Windows.UI.Notifications.ToastNotificationManager.GetDefault().CreateToastNotifier("com.squirrel.ForkPlus.ForkPlus");
				Windows.UI.Notifications.ToastNotification notification = new Windows.UI.Notifications.ToastNotification(document);
				notifier.Show(notification);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to show toast notification", ex);
			}
#else
			// 阶段 6：非 Windows 平台无 Toast 通知能力，仅记日志。
			Log.Info("Toast notification skipped on non-Windows platform: " + xmlPayload);
#endif
		}
	}
}
