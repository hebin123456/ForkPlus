using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
		// 阶段 6：用 ToastPayload 替代手写 Windows Toast XML，由 IToastNotificationService 实现按平台转换。
		// 无 launch 参数（多条通知汇总，点击仅打开通知中心，不需要跳转特定 URL）。
		SendNotification(new ToastPayload(title, body, silent: true));
	}

		private void SendToastNotification(GitServiceNotification notification)
		{
			// launch 参数编码 ToastNotification（threadId + url），点击通知时由
			// NotificationManager.ToastNotificationManagerCompat_OnActivated 回调解码并打开浏览器。
			string launchArg = ToastNotification.Coder.EncodeString(new ToastNotification(notification.Id, notification.TargetUrl));
			string title = notification.RepositoryFullName + " #" + notification.TargetId;
			string body = notification.Title ?? "";
			SendNotification(new ToastPayload(
				title: title,
				body: body,
				launchArgument: launchArg,
				imageUrl: notification.RepositoryAvatarUrl,
				silent: true));
		}

		/// <summary>
		/// 阶段 6：统一发送 Toast 通知入口。
		/// 替代原 SendWindowsNotification(string xmlString)（XML 是 Windows 专属格式）。
		/// 通过 ServiceLocator.Toast 投递 ToastPayload，由各平台 IToastNotificationService 实现转换：
		/// - Windows：WpfToastNotificationService → Toast XML → WinRT
		/// - Linux：LinuxToastNotificationService → notify-send
		/// - macOS：MacOsToastNotificationService → osascript
		/// ServiceLocator.Toast 为 null 时（启动早期 / 未注册）静默降级。
		/// </summary>
		public static void SendNotification(ToastPayload payload)
		{
			if (payload == null)
			{
				return;
			}
			if (Services.ServiceLocator.Toast != null)
			{
				try
				{
					Services.ServiceLocator.Toast.Show(payload);
				}
				catch (Exception ex)
				{
					Log.Error("Failed to show toast notification via IToastNotificationService", ex);
				}
				return;
			}
			// ServiceLocator 未初始化时的兜底（启动早期 / 设计期）。
			Log.Info("Toast notification skipped (ServiceLocator.Toast not registered): " + payload.Title);
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
