namespace ForkPlus.Services
{
	/// <summary>
	/// 图标键常量，用于业务层返回平台无关的图标标识符。
	/// UI 层通过这些键将图标解析为具体平台的资源。
	/// </summary>
	public static class IconKeys
	{
		// --- ChangeType / StatusType 图标键 ---
		public const string StatusAdd = "Status.Add";
		public const string StatusEdit = "Status.Edit";
		public const string StatusCopy = "Status.Copy";
		public const string StatusDelete = "Status.Delete";
		public const string StatusRemove = "Status.Remove";
		public const string StatusRename = "Status.Rename";
		public const string StatusTypeChanged = "Status.TypeChanged";
		public const string StatusUnmerged = "Status.Unmerged";
		public const string StatusUnknown = "Status.Unknown";
		public const string StatusIgnored = "Status.Ignored";
		public const string StatusNone = "Status.None";
		public const string Warning = "Warning";

		// --- RemoteType 图标键 ---
		public const string RemoteAzure = "Remote.Azure";
		public const string Azure = "Remote.Azure";
		public const string RemoteBitbucket = "Remote.Bitbucket";
		public const string Bitbucket = "Remote.Bitbucket";
		public const string RemoteGitea = "Remote.Gitea";
		public const string Gitea = "Remote.Gitea";
		public const string RemoteGithub = "Remote.Github";
		public const string GitHub = "Remote.Github";
		public const string RemoteGitlab = "Remote.Gitlab";
		public const string GitLab = "Remote.Gitlab";
		public const string RemoteGeneric = "Remote.Generic";
		public const string Remote = "Remote.Generic";

		// --- RemoteType Geometry Key（用于纯形状图标） ---
		public const string RemoteAzureGeometry = "Remote.AzureGeometry";
		public const string AzureGeometry = "Remote.AzureGeometry";
		public const string RemoteBitbucketGeometry = "Remote.BitbucketGeometry";
		public const string BitbucketGeometry = "Remote.BitbucketGeometry";
		public const string RemoteGiteaGeometry = "Remote.GiteaGeometry";
		public const string GiteaGeometry = "Remote.GiteaGeometry";
		public const string RemoteGitHubGeometry = "Remote.GitHubGeometry";
		public const string GitHubGeometry = "Remote.GitHubGeometry";
		public const string RemoteGitLabGeometry = "Remote.GitLabGeometry";
		public const string GitLabGeometry = "Remote.GitLabGeometry";
		public const string RemoteGenericGeometry = "Remote.GenericGeometry";
		public const string RemoteGeometry = "Remote.GenericGeometry";

		// --- Notification TargetType 图标键 ---
		public const string NotificationCommit = "Notification.Commit";
		public const string Commit = "Notification.Commit";
		public const string NotificationIssue = "Notification.Issue";
		public const string Issue = "Notification.Issue";
		public const string NotificationPullRequest = "Notification.PullRequest";
		public const string PullRequest = "Notification.PullRequest";
	}
}
