using System;
using System.Net;

namespace ForkPlus.Git
{
	public class RepositoryUrlBuilder
	{
		[Null]
		private string ServerUrlBase;

		[Null]
		private string _azureOwner;

		[Null]
		private string _azureProjectName;

		[Null]
		private string _visualStudioCollection;

		public GitUrl GitUrl { get; }

		public RemoteType RemoteType { get; }

		[Null]
		public string RepositoryWebpageUrl
		{
			get
			{
				string slug = GitUrl.Slug;
				if (slug != null)
				{
					string serverUrlBase = ServerUrlBase;
					if (serverUrlBase != null)
					{
						switch (RemoteType)
						{
						case RemoteType.Bitbucket:
							return serverUrlBase + "/" + slug;
						case RemoteType.BitbucketServer:
							slug = CreateBitbucketServerSlug(slug);
							return serverUrlBase + "/" + slug;
						case RemoteType.Gitea:
							return serverUrlBase + "/" + slug;
						case RemoteType.Github:
						case RemoteType.GithubEnterprise:
							return serverUrlBase + "/" + slug;
						case RemoteType.Gitlab:
						case RemoteType.GitlabServer:
							return serverUrlBase + "/" + slug;
						case RemoteType.Azure:
						{
							string repositoryName2 = GitUrl.RepositoryName;
							if (repositoryName2 != null)
							{
								string encodedProjectName2 = GetEncodedProjectName();
								if (encodedProjectName2 != null)
								{
									return "https://dev.azure.com/" + _azureOwner + "/" + encodedProjectName2 + "/_git/" + repositoryName2;
								}
							}
							return null;
						}
						case RemoteType.Visualstudio:
						{
							string repositoryName = GitUrl.RepositoryName;
							if (repositoryName != null)
							{
								string encodedProjectName = GetEncodedProjectName();
								if (encodedProjectName != null)
								{
									string encodedCollectionName = GetEncodedCollectionName();
									if (encodedCollectionName != null)
									{
										return "https://" + _azureOwner + ".visualstudio.com/" + encodedCollectionName + "/" + encodedProjectName + "/_git/" + repositoryName;
									}
									return "https://" + _azureOwner + ".visualstudio.com/" + encodedProjectName + "/_git/" + repositoryName;
								}
							}
							return null;
						}
						default:
							return null;
						}
					}
				}
				return null;
			}
		}

		[Null]
		public string IssuesUrl
		{
			get
			{
				string repositoryWebpageUrl = RepositoryWebpageUrl;
				if (repositoryWebpageUrl == null)
				{
					return null;
				}
				switch (RemoteType)
				{
				case RemoteType.Bitbucket:
					return repositoryWebpageUrl + "/issues/";
				case RemoteType.Gitea:
					return repositoryWebpageUrl + "/issues";
				case RemoteType.Github:
				case RemoteType.GithubEnterprise:
					return repositoryWebpageUrl + "/issues";
				case RemoteType.Gitlab:
				case RemoteType.GitlabServer:
					return repositoryWebpageUrl + "/-/issues/";
				case RemoteType.Azure:
				{
					string repositoryName2 = GitUrl.RepositoryName;
					if (repositoryName2 != null)
					{
						string encodedProjectName2 = GetEncodedProjectName();
						if (encodedProjectName2 != null)
						{
							return "https://dev.azure.com/" + _azureOwner + "/" + encodedProjectName2 + "/" + repositoryName2 + "/_workitems";
						}
					}
					return null;
				}
				case RemoteType.Visualstudio:
				{
					string repositoryName = GitUrl.RepositoryName;
					if (repositoryName != null)
					{
						string encodedProjectName = GetEncodedProjectName();
						if (encodedProjectName != null)
						{
							string encodedCollectionName = GetEncodedCollectionName();
							if (encodedCollectionName != null)
							{
								return "https://" + _azureOwner + ".visualstudio.com/" + encodedCollectionName + "/" + encodedProjectName + "/" + repositoryName + "/_workitems";
							}
							return "https://" + _azureOwner + ".visualstudio.com/" + encodedProjectName + "/" + repositoryName + "/_workitems";
						}
					}
					return null;
				}
				default:
					return null;
				}
			}
		}

		public string PullRequestsUrl
		{
			get
			{
				string repositoryWebpageUrl = RepositoryWebpageUrl;
				if (repositoryWebpageUrl == null)
				{
					return null;
				}
				switch (RemoteType)
				{
				case RemoteType.Bitbucket:
				case RemoteType.BitbucketServer:
					return repositoryWebpageUrl + "/pull-requests/";
				case RemoteType.Gitea:
					return repositoryWebpageUrl + "/pulls";
				case RemoteType.Github:
				case RemoteType.GithubEnterprise:
					return repositoryWebpageUrl + "/pulls";
				case RemoteType.Gitlab:
				case RemoteType.GitlabServer:
					return repositoryWebpageUrl + "/-/merge_requests";
				case RemoteType.Azure:
				{
					string repositoryName2 = GitUrl.RepositoryName;
					if (repositoryName2 != null)
					{
						string encodedProjectName2 = GetEncodedProjectName();
						if (encodedProjectName2 != null)
						{
							return "https://dev.azure.com/" + _azureOwner + "/" + encodedProjectName2 + "/_git/" + repositoryName2 + "/pullrequests";
						}
					}
					return null;
				}
				case RemoteType.Visualstudio:
				{
					string repositoryName = GitUrl.RepositoryName;
					if (repositoryName != null)
					{
						string encodedProjectName = GetEncodedProjectName();
						if (encodedProjectName != null)
						{
							string encodedCollectionName = GetEncodedCollectionName();
							if (encodedCollectionName != null)
							{
								return "https://" + _azureOwner + ".visualstudio.com/" + encodedCollectionName + "/" + encodedProjectName + "/_git/" + repositoryName + "/pullrequests";
							}
							return "https://" + _azureOwner + ".visualstudio.com/" + encodedProjectName + "/_git/" + repositoryName + "/pullrequests";
						}
					}
					return null;
				}
				default:
					return null;
				}
			}
		}

		public RepositoryUrlBuilder(string urlString)
			: this(new GitUrl(urlString))
		{
		}

		public RepositoryUrlBuilder(Remote remote)
			: this(remote.GitUrl, remote.Account?.ServerUrl, remote.Account?.ServiceType)
		{
		}

		public RepositoryUrlBuilder(GitUrl gitUrl, [Null] string serverUrlBase = null, RemoteType? predefinedRemoteType = null)
		{
			GitUrl = gitUrl;
			RemoteType = predefinedRemoteType ?? GitUrl.RemoteType;
			string slug = gitUrl.Slug;
			if (slug == null)
			{
				return;
			}
			string host = gitUrl.Host;
			if (host == null)
			{
				return;
			}
			string[] array = slug.Split(Consts.Chars.Slash);
			if (array.Length < 2)
			{
				return;
			}
			ServerUrlBase = "https://" + GitUrl.Host;
			if (RemoteType == RemoteType.Azure)
			{
				if (array.Length == 4)
				{
					switch (gitUrl.Protocol)
					{
					case GitUrl.NetworkProtocol.Https:
						_azureOwner = array[0];
						_azureProjectName = array[1];
						break;
					case GitUrl.NetworkProtocol.Ssh:
						_azureOwner = array[1];
						_azureProjectName = array[2];
						ServerUrlBase = "https://" + TrimStart(gitUrl.Host, "ssh.");
						break;
					}
				}
			}
			else if (RemoteType == RemoteType.Visualstudio)
			{
				switch (gitUrl.Protocol)
				{
				case GitUrl.NetworkProtocol.Https:
					if (array.Length == 3)
					{
						_azureOwner = host.Remove(host.LastIndexOf(".visualstudio.com"));
						_azureProjectName = array[0];
					}
					else if (array.Length == 4)
					{
						_azureOwner = host.Remove(host.LastIndexOf(".visualstudio.com"));
						_visualStudioCollection = array[0];
						_azureProjectName = array[1];
					}
					break;
				case GitUrl.NetworkProtocol.Ssh:
					if (array.Length == 4)
					{
						_azureOwner = array[1];
						_azureProjectName = array[2];
					}
					break;
				}
			}
			else if (RemoteType == RemoteType.BitbucketServer)
			{
				ServerUrlBase = serverUrlBase?.ToLower();
			}
			else if (RemoteType == RemoteType.GitlabServer)
			{
				ServerUrlBase = serverUrlBase?.ToLower();
			}
			else if (RemoteType == RemoteType.Gitea)
			{
				ServerUrlBase = serverUrlBase?.ToLower();
			}
		}

		[Null]
		private string CreateBitbucketServerSlug(string slug)
		{
			slug = TrimStart(slug, "scm/");
			string[] array = slug.Split(Consts.Chars.Slash);
			if (array.Length < 2)
			{
				return null;
			}
			if (array[0].StartsWith("~"))
			{
				return "users/" + array[0].Substring(1) + "/repos/" + array[1];
			}
			return "projects/" + array[0] + "/repos/" + array[1];
		}

		[Null]
		public string CreatePullRequestUrl(string branch)
		{
			if (RepositoryWebpageUrl == null)
			{
				return null;
			}
			string encodedBranchName = GetEncodedBranchName(branch);
			string encodedProjectName = GetEncodedProjectName();
			string encodedCollectionName = GetEncodedCollectionName();
			switch (RemoteType)
			{
			case RemoteType.Bitbucket:
				return RepositoryWebpageUrl + "/pull-requests/new?source=" + encodedBranchName;
			case RemoteType.BitbucketServer:
				return RepositoryWebpageUrl + "/pull-requests?create&sourceBranch=" + encodedBranchName;
			case RemoteType.Gitea:
				return RepositoryWebpageUrl + "/compare/" + encodedBranchName;
			case RemoteType.Github:
			case RemoteType.GithubEnterprise:
				return RepositoryWebpageUrl + "/compare/" + encodedBranchName + "?expand=1";
			case RemoteType.Gitlab:
			case RemoteType.GitlabServer:
				return RepositoryWebpageUrl + "/-/merge_requests/new?&merge_request%5Bsource_branch%5D=" + encodedBranchName;
			case RemoteType.Azure:
			{
				string repositoryName2 = GitUrl.RepositoryName;
				if (repositoryName2 == null)
				{
					return null;
				}
				if (encodedProjectName == null)
				{
					return null;
				}
				return "https://dev.azure.com/" + _azureOwner + "/" + encodedProjectName + "/_git/" + repositoryName2 + "/pullrequestcreate?sourceRef=" + encodedBranchName;
			}
			case RemoteType.Visualstudio:
			{
				string repositoryName = GitUrl.RepositoryName;
				if (repositoryName == null)
				{
					return null;
				}
				if (encodedProjectName != null)
				{
					if (encodedCollectionName != null)
					{
						return "https://" + _azureOwner + ".visualstudio.com/" + encodedCollectionName + "/" + encodedProjectName + "/_git/" + repositoryName + "/pullrequestcreate?sourceRef=" + encodedBranchName;
					}
					return "https://" + _azureOwner + ".visualstudio.com/" + encodedProjectName + "/_git/" + repositoryName + "/pullrequestcreate?sourceRef=" + encodedBranchName;
				}
				return null;
			}
			default:
				return null;
			}
		}

		[Null]
		public string CreateRevisionShaUrl(string sha)
		{
			if (RepositoryWebpageUrl == null)
			{
				return null;
			}
			string encodedProjectName = GetEncodedProjectName();
			string encodedCollectionName = GetEncodedCollectionName();
			switch (RemoteType)
			{
			case RemoteType.Bitbucket:
				return RepositoryWebpageUrl + "/commits/" + sha;
			case RemoteType.BitbucketServer:
				return RepositoryWebpageUrl + "/commits/" + sha;
			case RemoteType.Gitea:
				return RepositoryWebpageUrl + "/commit/" + sha;
			case RemoteType.Github:
			case RemoteType.GithubEnterprise:
				return RepositoryWebpageUrl + "/commit/" + sha;
			case RemoteType.Gitlab:
			case RemoteType.GitlabServer:
				return RepositoryWebpageUrl + "/-/commit/" + sha;
			case RemoteType.Azure:
			{
				string repositoryName2 = GitUrl.RepositoryName;
				if (repositoryName2 == null)
				{
					return null;
				}
				if (encodedProjectName == null)
				{
					return null;
				}
				return "https://dev.azure.com/" + _azureOwner + "/" + encodedProjectName + "/_git/" + repositoryName2 + "/commit/" + sha;
			}
			case RemoteType.Visualstudio:
			{
				string repositoryName = GitUrl.RepositoryName;
				if (repositoryName == null)
				{
					return null;
				}
				if (encodedProjectName != null)
				{
					if (encodedCollectionName != null)
					{
						return "https://" + _azureOwner + ".visualstudio.com/" + encodedCollectionName + "/" + encodedProjectName + "/_git/" + repositoryName + "/commit/" + sha;
					}
					return "https://" + _azureOwner + ".visualstudio.com/" + encodedProjectName + "/_git/" + repositoryName + "/commit/" + sha;
				}
				return null;
			}
			default:
				return null;
			}
		}

		[Null]
		public string CreateHttpsUrlString()
		{
			string repositoryWebpageUrl = RepositoryWebpageUrl;
			if (repositoryWebpageUrl == null)
			{
				return null;
			}
			UriBuilder uriBuilder = new UriBuilder(repositoryWebpageUrl);
			uriBuilder.Scheme = "https";
			if (uriBuilder.Port == 443)
			{
				uriBuilder.Port = -1;
			}
			uriBuilder.UserName = null;
			uriBuilder.Password = null;
			string encodedProjectName = GetEncodedProjectName();
			if (RemoteType == RemoteType.Azure)
			{
				if (encodedProjectName != null && GitUrl.RepositoryName != null)
				{
					return "https://dev.azure.com/" + _azureOwner + "/" + encodedProjectName + "/_git/" + GitUrl.RepositoryName;
				}
			}
			else if (RemoteType == RemoteType.Visualstudio && encodedProjectName != null && GitUrl.RepositoryName != null)
			{
				string encodedCollectionName = GetEncodedCollectionName();
				if (encodedCollectionName != null)
				{
					return "https://" + _azureOwner + ".visualstudio.com/" + encodedCollectionName + "/" + encodedProjectName + "/_git/" + GitUrl.RepositoryName;
				}
				return "https://" + _azureOwner + ".visualstudio.com/" + encodedProjectName + "/_git/" + GitUrl.RepositoryName;
			}
			return uriBuilder.ToString() + ".git";
		}

		[Null]
		public string CreateSshUrlString()
		{
			Uri uri = GitUrl.Uri;
			if ((object)uri == null)
			{
				return null;
			}
			UriBuilder uriBuilder = new UriBuilder(GitUrl.Uri);
			uriBuilder.Scheme = "";
			uriBuilder.Port = -1;
			uriBuilder.UserName = null;
			uriBuilder.Password = null;
			string text = uriBuilder.Path.Substring(1);
			string encodedProjectName = GetEncodedProjectName();
			if (RemoteType == RemoteType.Azure && GitUrl.RepositoryName != null)
			{
				if (encodedProjectName != null)
				{
					return "git@ssh." + uri.Host + ":v3/" + _azureOwner + "/" + encodedProjectName + "/" + GitUrl.RepositoryName;
				}
			}
			else if (RemoteType == RemoteType.Visualstudio && GitUrl.RepositoryName != null)
			{
				if (encodedProjectName != null)
				{
					return _azureOwner + "@vs-ssh.visualstudio.com:v3/" + _azureOwner + "/" + encodedProjectName + "/" + GitUrl.RepositoryName;
				}
			}
			else if (RemoteType == RemoteType.BitbucketServer)
			{
				text = TrimStart(text, "scm/");
				return "ssh://git@" + uriBuilder.Host + ":7999/" + text;
			}
			return "git@" + uriBuilder.Host + ":" + text;
		}

		private string GetEncodedBranchName(string branch)
		{
			string oldValue = "&";
			string oldValue2 = "#";
			string oldValue3 = "/";
			switch (RemoteType)
			{
			case RemoteType.Bitbucket:
			case RemoteType.BitbucketServer:
				return branch.Replace(oldValue2, "%23").Replace(oldValue, "%26");
			case RemoteType.Gitea:
			case RemoteType.Github:
			case RemoteType.GithubEnterprise:
				return WebUtility.UrlEncode(branch);
			case RemoteType.Gitlab:
			case RemoteType.GitlabServer:
				return branch.Replace(oldValue2, "%23").Replace(oldValue3, "%2F").Replace(oldValue, "%26");
			case RemoteType.Azure:
			case RemoteType.Visualstudio:
				return WebUtility.UrlEncode(branch);
			default:
				return branch;
			}
		}

		private static string TrimStart(string target, string trimString)
		{
			if (target.StartsWith(trimString))
			{
				return target.Substring(trimString.Length);
			}
			return target;
		}

		private string GetEncodedProjectName()
		{
			string oldValue = " ";
			return RemoteType switch
			{
				RemoteType.Azure => _azureProjectName?.Replace(oldValue, "%20"), 
				RemoteType.Visualstudio => _azureProjectName?.Replace(oldValue, "%20"), 
				_ => null, 
			};
		}

		private string GetEncodedCollectionName()
		{
			string oldValue = " ";
			if (RemoteType == RemoteType.Visualstudio)
			{
				return _visualStudioCollection?.Replace(oldValue, "%20");
			}
			return null;
		}
	}
}
