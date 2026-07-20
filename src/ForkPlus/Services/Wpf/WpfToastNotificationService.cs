using System;
using CommunityToolkit.WinUI.Notifications;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// WPF 平台的 Toast 通知服务，封装 WinRT Toast API。
	/// Phase 0.5：同时监听 ToastNotificationManagerCompat.OnActivated 并转发到
	/// IToastNotificationService.OnActivated 事件，让 Core/NotificationManager 通过
	/// ServiceLocator.Toast.OnActivated 订阅点击回调，无需直接依赖 WinRT API。
	/// </summary>
	public class WpfToastNotificationService : IToastNotificationService
	{
		private readonly Action<string> _onActivatedHandler;

		public event Action<string> OnActivated;

		public WpfToastNotificationService()
		{
			_onActivatedHandler = OnActivatedInternal;
			ToastNotificationManagerCompat.OnActivated += _onActivatedHandler;
		}

		private void OnActivatedInternal(ToastNotificationActivatedEventArgsCompat e)
		{
			string argument = e?.Argument;
			if (argument != null)
			{
				OnActivated?.Invoke(argument);
			}
		}

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
