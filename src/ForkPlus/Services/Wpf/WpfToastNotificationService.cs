using System;
using CommunityToolkit.WinUI.Notifications;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// WPF 平台的 Toast 通知服务，封装 WinRT Toast API。
	/// </summary>
	public class WpfToastNotificationService : IToastNotificationService
	{
		public void Show(string xmlPayload)
		{
			try
			{
				XmlDocument document = new XmlDocument();
				document.LoadXml(xmlPayload);
				ToastNotifier notifier = ToastNotificationManager.GetDefault().CreateToastNotifier("com.squirrel.ForkPlus.ForkPlus");
				Windows.UI.Notifications.ToastNotification notification = new Windows.UI.Notifications.ToastNotification(document);
				notifier.Show(notification);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to show toast notification", ex);
			}
		}
	}
}
