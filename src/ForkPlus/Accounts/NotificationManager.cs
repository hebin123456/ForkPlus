using System;
using System.Collections.Generic;
using System.Net;
#if WINDOWS
using CommunityToolkit.WinUI.Notifications;
#endif
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Utils.Http;
using Avalonia.Threading;

namespace ForkPlus.Accounts
{
	internal class NotificationManager
	{
		public static readonly NotificationManager Current = new NotificationManager();

		private static readonly TimeSpan FirstUpdateDelay = TimeSpan.FromSeconds(5.0);

		private static readonly TimeSpan UpdateInterval = TimeSpan.FromMinutes(15.0);

		private readonly JobQueue _jobQueue = new JobQueue();

		[Null]
		private Job _activeJob;

		private bool _isActive;

		private bool _isUpdating;

		private GitServiceNotification[] _notifications = new GitServiceNotification[0];

		public bool IsActive
		{
			get
			{
				return _isActive;
			}
			private set
			{
				if (_isActive != value)
				{
					_isActive = value;
					this.IsActiveChanged?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		public bool IsUpdating
		{
			get
			{
				return _isUpdating;
			}
			private set
			{
				if (_isUpdating != value)
				{
					_isUpdating = value;
					this.IsUpdatingChanged?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		public GitServiceNotification[] Notifications
		{
			get
			{
				return _notifications;
			}
			private set
			{
				_notifications = value;
				this.NotificationsChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		public event EventHandler IsActiveChanged;

		public event EventHandler IsUpdatingChanged;

		public event EventHandler NotificationsChanged;

		public NotificationManager()
		{
			// 阶段 5：ToastNotificationManagerCompat 是 Windows-only，非 Windows 平台跳过注册。
#if WINDOWS
			if (OperatingSystem.IsWindows())
			{
				try
				{
					ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;
				}
				catch (Exception ex)
				{
					Log.Error("Failed to subscribe ToastNotificationManagerCompat.OnActivated", ex);
				}
			}
#endif
			if (Services.ServiceLocator.Timer != null)
			{
				Services.ServiceLocator.Timer.Interval = FirstUpdateDelay;
				Services.ServiceLocator.Timer.Tick += _timer_Tick;
				Services.ServiceLocator.Timer.Start();
			}
		}

		public void UnsetUnread(GitServiceNotification notification)
		{
			UnsetUnread(notification.Id);
		}

		public void Refresh()
		{
			if (Services.ServiceLocator.Timer != null)
			{
				Services.ServiceLocator.Timer.Interval = UpdateInterval;
			}
			_activeJob?.Monitor.Cancel();
			List<Account> notificationAccounts = AccountManager.Current.Accounts.Filter((Account x) => x.EnableNotifications && x.Service is INotificationGitService);
			if (notificationAccounts.Count == 0)
			{
				IsActive = false;
				return;
			}
			IsActive = true;
			IsUpdating = true;
			_activeJob = _jobQueue.Add(ServiceLocator.Localization.Current("Refresh Notifications"), delegate(JobMonitor monitor)
			{
				GitServiceNotification newNotification = null;
				int newNotificationsCount = 0;
				List<GitServiceNotification> list = new List<GitServiceNotification>();
				foreach (Account item in notificationAccounts)
				{
					ServiceResult<GitServiceNotification[]> serviceResult = (item.Service as INotificationGitService).GetNotifications().LoadNext();
					if (!serviceResult.Succeeded)
					{
						Log.Error(serviceResult.Error.FriendlyMessage);
					}
					else
					{
						DateTime notificationsUpdatedAt = item.NotificationsUpdatedAt;
						GitServiceNotification[] result2 = serviceResult.Result;
						foreach (GitServiceNotification gitServiceNotification in result2)
						{
							if (notificationsUpdatedAt != DateTime.MinValue && gitServiceNotification.Date > notificationsUpdatedAt && gitServiceNotification.Unread)
							{
								newNotification = gitServiceNotification;
								newNotificationsCount++;
							}
							list.Add(gitServiceNotification);
						}
						item.NotificationsUpdatedAt = DateTime.Now;
					}
				}
				list.Sort((GitServiceNotification x, GitServiceNotification y) => -1 * x.Date.CompareTo(y.Date));
				GitServiceNotification[] result = list.ToArray();
				if (!monitor.IsCanceled)
				{
					// 阶段 5：Services.Dispatcher → Avalonia.Threading.Dispatcher（using 已导入）。
					Dispatcher.UIThread.Post(delegate
					{
						AccountManager.Current.Save();
						IsUpdating = false;
						_activeJob = null;
						Notifications = result;
						if (newNotificationsCount > 0)
						{
							if (newNotificationsCount == 1 && newNotification != null)
							{
								SendToastNotification(newNotification);
							}
							else
							{
								SendToastNotification(newNotificationsCount);
							}
							Log.Info($"Received {newNotificationsCount} new notifications");
						}
					});
				}
			});
		}

		private void _timer_Tick(object sender, EventArgs e)
		{
			Refresh();
		}

#if WINDOWS
		private void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat e)
		{
			Log.Info("Activated toast notification");
			string text = WebUtility.HtmlDecode(e.Argument);
			string text2 = "ai-review:";
			if (text.StartsWith(text2))
			{
				FindAiCodeReviewWindowAndActivate(text.Substring(text2.Length).Trim());
				return;
			}
			ToastNotification toastNotificaton = ToastNotification.Coder.DecodeString(text);
			if (toastNotificaton != null)
			{
				new Uri(toastNotificaton.Url).OpenInBrowser();
				// 阶段 5：Services.Dispatcher → Avalonia.Threading.Dispatcher（using 已导入）。
				Dispatcher.UIThread.Post(delegate
				{
					UnsetUnread(toastNotificaton.ThreadId);
				});
				return;
			}
			// 阶段 5：Services.Dispatcher → Avalonia.Threading.Dispatcher（using 已导入）。
			Dispatcher.UIThread.Post(delegate
			{
				var windowManager = Services.ServiceLocator.WindowManager;
				if (windowManager != null)
				{
					windowManager.ActivateAndShowNotifications();
				}
			});
		}
#endif

		private void UnsetUnread(string notificationId)
		{
			int? num = Notifications.IndexOfItem((GitServiceNotification x) => x.Id == notificationId);
			if (num.HasValue)
			{
				int valueOrDefault = num.GetValueOrDefault();
				GitServiceNotification gitServiceNotification = Notifications[valueOrDefault];
				GitServiceNotification gitServiceNotification2 = new GitServiceNotification(gitServiceNotification.Id, gitServiceNotification.Title, gitServiceNotification.Date, unread: false, gitServiceNotification.RepositoryFullName, gitServiceNotification.RepositoryAvatarUrl, gitServiceNotification.TargetType, gitServiceNotification.TargetId, gitServiceNotification.TargetUrl);
				Notifications[valueOrDefault] = gitServiceNotification2;
			}
		}

		private void SendToastNotification(int newNotificationsCount)
	{
		string title = ServiceLocator.Localization.Current("New Notifications");
		string body = ServiceLocator.Localization.FormatCurrent("You've got {0} new notifications", newNotificationsCount);
		SendWindowsNotification($"<?xml version=\"1.0\" encoding =\"utf-8\" ?>\n<toast>\n<audio silent=\"true\"/>\n<visual>\n    <binding template=\"ToastGeneric\">\n        <text hint-maxLines=\"1\">{WebUtility.HtmlEncode(title)}</text>\n        <text>{WebUtility.HtmlEncode(body)}</text>\n    </binding>\n</visual>\n</toast>\n");
	}

		private void SendToastNotification(GitServiceNotification notification)
		{
			string text = WebUtility.HtmlEncode(ToastNotification.Coder.EncodeString(new ToastNotification(notification.Id, notification.TargetUrl)));
			string text2 = WebUtility.HtmlEncode(notification.RepositoryFullName + " #" + notification.TargetId);
			string text3 = WebUtility.HtmlEncode(notification.Title ?? "");
			string text4 = WebUtility.HtmlEncode(notification.RepositoryAvatarUrl ?? "");
			SendWindowsNotification($"<?xml version=\"1.0\" encoding =\"utf-8\" ?>\n<toast launch=\"{text}\" >\n<audio silent=\"true\"/>\n<visual>\n    <binding template=\"ToastGeneric\">\n        <text hint-maxLines=\"1\" >{text2}</text>\n        <text>{text3}</text>\n        <image placement=\"hero\" src =\"{text4}\" />\n    </binding>\n</visual>\n</toast>\n");
		}

		public static void SendWindowsNotification(string xmlString)
		{
			if (Services.ServiceLocator.Toast != null)
			{
				Services.ServiceLocator.Toast.Show(xmlString);
				return;
			}
			// 阶段 5：WinRT Toast 仅 Windows 平台可用，非 Windows 平台静默降级（仅记日志）。
			if (!OperatingSystem.IsWindows())
			{
				Log.Info("Toast notification skipped on non-Windows platform");
				return;
			}
			// 回退：直接使用 WinRT API（ServiceLocator 未初始化时）。
			// 用反射延迟加载 WinRT 类型，避免在非 Windows 平台编译/加载时引发 TypeLoadException。
			try
			{
				Type xmlDocType = Type.GetType("Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom, ContentType=WindowsRuntime", throwOnError: false);
				if (xmlDocType == null)
				{
					Log.Info("WinRT XmlDocument unavailable, toast notification skipped");
					return;
				}
				object document = Activator.CreateInstance(xmlDocType);
				xmlDocType.GetMethod("LoadXml")?.Invoke(document, new object[] { xmlString });

				Type managerType = Type.GetType("Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType=WindowsRuntime", throwOnError: false);
				if (managerType == null)
				{
					return;
				}
				object defaultObj = managerType.GetProperty("Default")?.GetValue(null);
				object notifier = managerType.GetMethod("CreateToastNotifier", new[] { typeof(string) })?.Invoke(defaultObj, new object[] { "com.squirrel.ForkPlus.ForkPlus" });

				Type toastType = Type.GetType("Windows.UI.Notifications.ToastNotification, Windows.UI.Notifications, ContentType=WindowsRuntime", throwOnError: false);
				if (toastType == null)
				{
					return;
				}
				object notification = Activator.CreateInstance(toastType, document);
				notifier.GetType().GetMethod("Show")?.Invoke(notifier, new[] { notification });
			}
			catch (Exception ex)
			{
				Log.Error("Failed to show toast notification", ex);
			}
		}

		private void FindAiCodeReviewWindowAndActivate(string windowTitle)
		{
			var windowManager = Services.ServiceLocator.WindowManager;
			if (windowManager != null)
			{
				windowManager.DispatchToUiThread(delegate
				{
					windowManager.TryActivateWindowByTitle(windowTitle);
				});
			}
		}
	}
}
