using System.Windows.Media;
using ForkPlus.UI;

namespace ForkPlus.Accounts
{
	public static class GitServiceNotificationTargetTypeExtension
	{
		public static ImageSource Icon(this GitServiceNotificationTargetType targetType)
		{
			return targetType switch
			{
				GitServiceNotificationTargetType.Commit => Theme.RevisionIcon, 
				GitServiceNotificationTargetType.Issue => Theme.IssueIcon, 
				GitServiceNotificationTargetType.PullRequest => Theme.PullRequestIcon, 
				_ => Theme.IssueIcon, 
			};
		}
	}
}
