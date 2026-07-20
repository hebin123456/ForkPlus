namespace ForkPlus.Accounts
{
	public interface INotificationGitService
	{
		IPaged<GitServiceNotification> GetNotifications();
	}
}
