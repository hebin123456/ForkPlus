using System;

namespace ForkPlus.Accounts
{
	public class GitServiceNotification
	{
		public string Id { get; }

		public string Title { get; }

		public DateTime Date { get; }

		public bool Unread { get; }

		public string RepositoryFullName { get; }

		public string RepositoryAvatarUrl { get; }

		public GitServiceNotificationTargetType TargetType { get; }

		[Null]
		public string TargetId { get; }

		public string TargetUrl { get; }

		public GitServiceNotification(string id, string title, DateTime date, bool unread, string repositoryFullName, string repositoryAvatarUrl, GitServiceNotificationTargetType targetType, string targetId, string targetUrl)
		{
			Id = id;
			Title = title;
			Date = date;
			Unread = unread;
			RepositoryFullName = repositoryFullName;
			RepositoryAvatarUrl = repositoryAvatarUrl;
			TargetType = targetType;
			TargetId = targetId;
			TargetUrl = targetUrl;
		}
	}
}
