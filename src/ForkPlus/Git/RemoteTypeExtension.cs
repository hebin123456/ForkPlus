using System.Windows.Media;
using ForkPlus.UI;

namespace ForkPlus.Git
{
	public static class RemoteTypeExtension
	{
		public static ImageSource Icon(this RemoteType remoteType)
		{
			switch (remoteType)
			{
			case RemoteType.Azure:
			case RemoteType.Visualstudio:
				return Theme.AzureIcon;
			case RemoteType.Bitbucket:
			case RemoteType.BitbucketServer:
				return Theme.BitbucketIcon;
			case RemoteType.Gitea:
				return Theme.GiteaIcon;
			case RemoteType.Github:
			case RemoteType.GithubEnterprise:
				return Theme.GitHubIcon;
			case RemoteType.Gitlab:
			case RemoteType.GitlabServer:
				return Theme.GitLabIcon;
			default:
				return Theme.RemoteIcon;
			}
		}

		public static Geometry IconGeometry(this RemoteType remoteType)
		{
			switch (remoteType)
			{
			case RemoteType.Azure:
			case RemoteType.Visualstudio:
				return Theme.AzureGeometry;
			case RemoteType.Bitbucket:
			case RemoteType.BitbucketServer:
				return Theme.BitbucketGeometry;
			case RemoteType.Gitea:
				return Theme.GiteaGeometry;
			case RemoteType.Github:
			case RemoteType.GithubEnterprise:
				return Theme.GitHubGeometry;
			case RemoteType.Gitlab:
			case RemoteType.GitlabServer:
				return Theme.GitLabGeometry;
			default:
				return Theme.RemoteGeometry;
			}
		}

		[Null]
		public static string FriendlyName(this RemoteType remoteType)
		{
			return remoteType switch
			{
				RemoteType.Azure => "Azure", 
				RemoteType.Bitbucket => "Bitbucket", 
				RemoteType.BitbucketServer => "Bitbucket Server", 
				RemoteType.Gitea => "Gitea", 
				RemoteType.Github => "GitHub", 
				RemoteType.GithubEnterprise => "GitHub Enterprise Server", 
				RemoteType.Gitlab => "GitLab", 
				RemoteType.GitlabServer => "GitLab Server", 
				RemoteType.Visualstudio => "Visualstudio", 
				_ => null, 
			};
		}
	}
}
