using System;
using System.Collections.Generic;
using System.Net;
using ForkPlus.Jobs;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;

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
			// Migration note：WPF 侧订阅 Microsoft.Toolkit.Uwp.Notifications 的 ToastNotificationManagerCompat.OnActivated，
			// 在用户点击 Toast 时回调 ToastNotificationManagerCompat_OnActivated（深链到通知窗口/AI Review 窗口）。
			// Avalonia（跨平台）无 WinRT Toast API，项目内也无对应 shim；toast 激活回调暂缺失，
			// 待 IToastNotificationService 增加 Activated 事件后接回（处理逻辑保留在 OnToastActivated）。
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
			_activeJob = _jobQueue.Add(PreferencesLocalization.Current("Refresh Notifications"), delegate(JobMonitor monitor)
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
					Services.ServiceLocator.Dispatcher.Post(delegate
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

		private void OnToastActivated(string argument)
		{
			Log.Info("Activated toast notification");
			string text = WebUtility.HtmlDecode(argument);
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
				Services.ServiceLocator.Dispatcher.Post(delegate
				{
					UnsetUnread(toastNotificaton.ThreadId);
				});
				return;
			}
			Services.ServiceLocator.Dispatcher.Post(delegate
			{
				var windowManager = Services.ServiceLocator.WindowManager;
				if (windowManager != null)
				{
					windowManager.ActivateAndShowNotifications();
				}
			});
		}

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
		string title = PreferencesLocalization.Current("New Notifications");
		string body = PreferencesLocalization.FormatCurrent("You've got {0} new notifications", newNotificationsCount);
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
			// Migration note：WPF 侧在 ServiceLocator 未初始化时直接回退 WinRT
			// Windows.Data.Xml.Dom / Windows.UI.Notifications API 弹 Toast；
			// Avalonia（跨平台，TFM 未启用 Windows）无这些命名空间，回退分支移除，仅记录日志。
			Log.Warn("Toast notification service is not available, notification dropped");
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
