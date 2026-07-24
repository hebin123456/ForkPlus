using System;
using System.ComponentModel;
using Avalonia.Media;
using ForkPlus.Accounts;

namespace ForkPlus.UI.UserControls
{
	public class NotificationViewModel : INotifyPropertyChanged
	{
		public GitServiceNotification Notification { get; }

		public string Title => Notification.RepositoryFullName;

		public string TargetId { get; }

		[Null]
		public string RepositoryAvatarUrl => Notification.RepositoryAvatarUrl;

		public string Description => Notification.Title;

		public DateTime DateTime => Notification.Date;

		public IImage TargetTypeIcon => Notification.TargetType.Icon();

		public string TargetUrl => Notification.TargetUrl;

		public bool Unread => Notification.Unread;

		public event PropertyChangedEventHandler PropertyChanged;

		public NotificationViewModel(GitServiceNotification notification)
		{
			Notification = notification;
			TargetId = UserFriendlyId(notification);
		}

		private static string UserFriendlyId(GitServiceNotification notification)
		{
			return notification.TargetType switch
			{
				GitServiceNotificationTargetType.Commit => notification.TargetId.Substring(0, 7), 
				GitServiceNotificationTargetType.Issue => "#" + notification.TargetId, 
				GitServiceNotificationTargetType.PullRequest => "#" + notification.TargetId, 
				_ => "#" + notification.TargetId, 
			};
		}
	}
}
