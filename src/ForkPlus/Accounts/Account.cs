using System;
using System.Diagnostics;
using ForkPlus.Git;
using ForkPlus.Utils.Http;

namespace ForkPlus.Accounts
{
	[DebuggerDisplay("{ServerUrl, nq} ({Username, nq})")]
	public class Account
	{
		public static class Keys
		{
			public const string ServiceType = "serviceType";

			public const string ServiceTypeBitbucket = "Bitbucket";

			public const string ServiceTypeBitbucketServer = "BitbucketServer";

			public const string ServiceTypeGitea = "Gitea";

			public const string ServiceTypeGitHub = "GitHub";

			public const string ServiceTypeGitHubEnterprise = "GitHubEnterprise";

			public const string ServiceTypeGitLab = "GitLab";

			public const string ServiceTypeGitLabServer = "GitLabServer";

			public const string AuthenticationType = "authenticationType";

			public const string AuthenticationTypeAccessToken = "AccessToken";

			public const string AuthenticationTypeOAuth = "OAuth";

			public const string ServerUrl = "serverUrl";

			public const string Username = "username";

			public const string AvatarUrl = "avatarUrl";

			public const string Email = "email";

			public const string EnableNotifications = "enableNotifications";

			public const string NotificationsUpdatedAt = "notificationsUpdatedAt";
		}

		private bool _enableNotifications;

		private DateTime _notificationsUpdatedAt;

		public RemoteType ServiceType { get; }

		public AuthenticationType AuthenticationType { get; }

		public string ServerUrl { get; }

		[Null]
		public string Email { get; }

		public string Username { get; }

		[Null]
		public string AvatarUrl { get; }

		public string Host { get; }

		public GitService Service { get; }

		public bool EnableNotifications
		{
			get
			{
				lock (Service)
				{
					return _enableNotifications;
				}
			}
			set
			{
				lock (Service)
				{
					_enableNotifications = value;
				}
			}
		}

		public DateTime NotificationsUpdatedAt
		{
			get
			{
				lock (Service)
				{
					return _notificationsUpdatedAt;
				}
			}
			set
			{
				lock (Service)
				{
					_notificationsUpdatedAt = value;
				}
			}
		}

		public Account(RemoteType serviceType, AuthenticationType authenticationType, string serverUrl, [Null] string email, string username, [Null] string avatarUrl, bool enableNotifications = false, [Null] DateTime? notificationsUpdatedAt = null)
		{
			ServiceType = serviceType;
			AuthenticationType = authenticationType;
			Email = email;
			Username = username;
			ServerUrl = serverUrl;
			AvatarUrl = avatarUrl;
			_enableNotifications = enableNotifications;
			_notificationsUpdatedAt = notificationsUpdatedAt ?? DateTime.MinValue;
			Host = new Uri(serverUrl).Host;
			switch (serviceType)
			{
			case RemoteType.Bitbucket:
			{
				IRestServiceAuthentication authentication6;
				if (authenticationType == AuthenticationType.OAuth)
				{
					authentication6 = new BitbucketOAuthAuthentication(serverUrl, username);
				}
				else if (email != null)
				{
					authentication6 = new BitbucketBasicAuthentication(serverUrl, email, username);
				}
				else
				{
					authentication6 = new BasicAuthentication(serverUrl, username);
				}
				Service = new BitbucketService(new Connection("https://api.bitbucket.org", authentication6));
				break;
			}
			case RemoteType.BitbucketServer:
			{
				PrivateAccessTokenAuthentication authentication5 = new PrivateAccessTokenAuthentication(serverUrl, username);
				Service = new BitbucketServerService(new Connection(ServerUrl, authentication5));
				break;
			}
			case RemoteType.Gitea:
			{
				PrivateAccessTokenAuthentication authentication7 = new PrivateAccessTokenAuthentication(serverUrl, username);
				Service = new GiteaService(new Connection(ServerUrl, authentication7));
				break;
			}
			case RemoteType.Github:
			{
				IRestServiceAuthentication restServiceAuthentication2;
				if (authenticationType != AuthenticationType.OAuth)
				{
					IRestServiceAuthentication restServiceAuthentication = new GitHubAccessTokenAuthentication(serverUrl, username);
					restServiceAuthentication2 = restServiceAuthentication;
				}
				else
				{
					IRestServiceAuthentication restServiceAuthentication = new GitHubOAuthAuthentication(serverUrl, username);
					restServiceAuthentication2 = restServiceAuthentication;
				}
				IRestServiceAuthentication authentication4 = restServiceAuthentication2;
				Service = new GitHubService(new Connection("https://api.github.com", authentication4));
				break;
			}
			case RemoteType.GithubEnterprise:
			{
				PrivateAccessTokenAuthentication authentication3 = new PrivateAccessTokenAuthentication(serverUrl, username);
				Service = new GitHubService(new Connection(ServerUrl, authentication3), gitHubEnterprise: true);
				break;
			}
			case RemoteType.Gitlab:
			{
				GitLabPrivateAccessTokenAuthentication authentication2 = new GitLabPrivateAccessTokenAuthentication(serverUrl, username);
				Service = new GitLabService(new Connection(ServerUrl, authentication2));
				break;
			}
			case RemoteType.GitlabServer:
			{
				GitLabPrivateAccessTokenAuthentication authentication = new GitLabPrivateAccessTokenAuthentication(serverUrl, username);
				Service = new GitLabService(new Connection(ServerUrl, authentication), gitlabServer: true);
				break;
			}
			}
		}
	}
}
