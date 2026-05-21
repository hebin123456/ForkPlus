using System;
using System.Collections.Generic;
using System.Net;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.WinUI.Notifications;
using ForkPlus.Jobs;
using ForkPlus.UI;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace ForkPlus.Accounts
{
	internal class NotificationManager
	{
		public static readonly NotificationManager Current = new NotificationManager();

		private static readonly TimeSpan FirstUpdateDelay = TimeSpan.FromSeconds(5.0);

		private static readonly TimeSpan UpdateInterval = TimeSpan.FromMinutes(15.0);

		private readonly JobQueue _jobQueue = new JobQueue();

		private readonly DispatcherTimer _dispatcherTimer = new DispatcherTimer();

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
			ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;
			_dispatcherTimer.Interval = FirstUpdateDelay;
			_dispatcherTimer.Tick += _dispatcherTimer_Tick;
			_dispatcherTimer.Start();
		}

		public void UnsetUnread(GitServiceNotification notification)
		{
			UnsetUnread(notification.Id);
		}

		public void Refresh()
		{
			_dispatcherTimer.Interval = UpdateInterval;
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
					_dispatcherTimer.Dispatcher.Async(delegate
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

		private void _dispatcherTimer_Tick(object sender, EventArgs e)
		{
			Refresh();
		}

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
				_dispatcherTimer.Dispatcher.Async(delegate
				{
					UnsetUnread(toastNotificaton.ThreadId);
				});
				return;
			}
			_dispatcherTimer.Dispatcher.Async(delegate
			{
				MainWindow instance = MainWindow.Instance;
				if (instance != null)
				{
					instance.Activate();
					instance.ShowNotificationManager();
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
			SendWindowsNotification($"<?xml version=\"1.0\" encoding =\"utf-8\" ?>\n<toast>\n<audio silent=\"true\"/>\n<visual>\n    <binding template=\"ToastGeneric\">\n        <text hint-maxLines=\"1\">New Notifications</text>\n        <text>You've got {newNotificationsCount} new notifications</text>\n    </binding>\n</visual>\n</toast>\n");
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
			try
			{
				XmlDocument document = new XmlDocument();
				document.LoadXml(xmlString);
				ToastNotifier notifier = ToastNotificationManager.GetDefault().CreateToastNotifier("com.squirrel.ForkPlus.ForkPlus");
				Windows.UI.Notifications.ToastNotification notification = new Windows.UI.Notifications.ToastNotification(document);
				notifier.Show(notification);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to show toast notification", ex);
			}
		}

		private void FindAiCodeReviewWindowAndActivate(string windowTitle)
		{
			Application.Current?.Dispatcher.Async(delegate
			{
				WindowCollection windowCollection = Application.Current?.Windows;
				if (windowCollection == null)
				{
					return;
				}
				foreach (object item in windowCollection)
				{
					if (item is AiCodeReviewWindow aiCodeReviewWindow && aiCodeReviewWindow.Title == windowTitle)
					{
						aiCodeReviewWindow.Activate();
						break;
					}
				}
			});
		}
	}
}
