using System;
using System.Collections.Generic;
using System.Text;
using ForkPlus.Git;
using ForkPlus.Utils.Http;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Accounts
{
	public class GitHubService : GitService, INotificationGitService
	{
		private class Coder
		{
			[Null]
			public static User DecodeUser(JObject json)
			{
				string @string = json.GetString("login");
				if (@string != null)
				{
					string string2 = json.GetString("avatar_url");
					if (string2 != null)
					{
						string string3 = json.GetString("html_url");
						if (string3 != null)
						{
							string displayName = json.GetString("name") ?? @string;
							return new User(@string, displayName, string2, string3);
						}
					}
				}
				Log.Warn("Cannot parse User json");
				return null;
			}

			[Null]
			public static GitServiceRepository[] DecodeGitServiceRepositoryArray(JArray jArray)
			{
				List<GitServiceRepository> list = new List<GitServiceRepository>(jArray.Count);
				foreach (JToken item in jArray)
				{
					GitServiceRepository gitServiceRepository = DecodeGitServiceRepository(item as JObject);
					if (gitServiceRepository != null)
					{
						list.Add(gitServiceRepository);
						continue;
					}
					return null;
				}
				return list.ToArray();
			}

			[Null]
			private static GitServiceRepository DecodeGitServiceRepository([Null] JObject json)
			{
				if (json != null)
				{
					string @string = json.GetString("name");
					if (@string != null)
					{
						string string2 = json.GetString("clone_url");
						if (string2 != null)
						{
							string string3 = json.GetString("ssh_url");
							if (string3 != null)
							{
								string string4 = json.GetString("owner", "login");
								if (string4 != null)
								{
									string string5 = json.GetString("owner", "avatar_url");
									if (string5 != null)
									{
										return new GitServiceRepository(@string, string4, string5, string2, string3);
									}
								}
							}
						}
					}
				}
				Log.Warn("Cannot parse GitServiceRepository");
				return null;
			}

			[Null]
			public static GitServiceNotification[] DecodeGitServiceNotificationArray(JArray jArray)
			{
				List<GitServiceNotification> list = new List<GitServiceNotification>(jArray.Count);
				foreach (JToken item in jArray)
				{
					GitServiceNotification gitServiceNotification = DecodeGitServiceNotification(item as JObject);
					if (gitServiceNotification != null)
					{
						list.Add(gitServiceNotification);
					}
				}
				return list.ToArray();
			}

			[Null]
			private static GitServiceNotification DecodeGitServiceNotification([Null] JObject json)
			{
				if (json != null)
				{
					string @string = json.GetString("id");
					if (@string != null && json["subject"] is JObject jObject)
					{
						string string2 = jObject.GetString("title");
						if (string2 != null)
						{
							string string3 = jObject.GetString("url");
							if (string3 != null && json["repository"] is JObject json2)
							{
								string string4 = json2.GetString("full_name");
								if (string4 != null)
								{
									string string5 = json2.GetString("owner", "avatar_url");
									if (string5 != null)
									{
										bool? flag = json["unread"]?.Value<bool>();
										if (flag.HasValue)
										{
											bool valueOrDefault = flag.GetValueOrDefault();
											DateTime? dateTime = json["updated_at"]?.Value<DateTime>();
											if (dateTime.HasValue)
											{
												DateTime date = dateTime.GetValueOrDefault().ToLocalTime();
												GitServiceNotificationTargetType? gitServiceNotificationTargetType = DecodeGitServiceNotificationTargetType(jObject["type"]);
												if (gitServiceNotificationTargetType.HasValue)
												{
													GitServiceNotificationTargetType valueOrDefault2 = gitServiceNotificationTargetType.GetValueOrDefault();
													string text = DecodeTargetId(string3);
													if (text == null)
													{
														Log.Warn("Cannot parse targetId in '" + string3 + "'");
														return null;
													}
													string targetUrl = string3.Replace("://api.", "://").Replace("/repos/", "/").Replace("/commits/", "/commit/")
														.Replace("/pulls/", "/pull/");
													return new GitServiceNotification(@string, string2, date, valueOrDefault, string4, string5, valueOrDefault2, text, targetUrl);
												}
												return null;
											}
										}
									}
								}
							}
						}
					}
				}
				Log.Warn("Cannot parse GitServiceNotification");
				return null;
			}

			[Null]
			private static string DecodeTargetId(string urlString)
			{
				int num = urlString.LastIndexOf("/");
				if (num != -1 && urlString.Length > num + 1)
				{
					return urlString.Substring(num + 1);
				}
				return null;
			}

			private static GitServiceNotificationTargetType? DecodeGitServiceNotificationTargetType(JToken jToken)
			{
				string text = jToken?.Value<string>();
				switch (text)
				{
				case "Commit":
					return GitServiceNotificationTargetType.Commit;
				case "Issue":
					return GitServiceNotificationTargetType.Issue;
				case "PullRequest":
					return GitServiceNotificationTargetType.PullRequest;
				default:
					Log.Warn("Cannot parse notification type '" + text + "'");
					return null;
				}
			}

			[Null]
			public static IssuesResponse DecodeIssuesResponse(JObject json)
			{
				if (!(json["items"] is JArray jArray))
				{
					Log.Warn("Cannot parse 'items'");
					return null;
				}
				List<Issue> list = new List<Issue>(jArray.Count);
				foreach (JToken item in jArray)
				{
					Issue issue = DecodeIssue(item as JObject);
					if (issue != null)
					{
						list.Add(issue);
					}
				}
				return new IssuesResponse(list.ToArray());
			}

			[Null]
			private static Issue DecodeIssue(JObject json)
			{
				if (json != null)
				{
					string @string = json.GetString("number");
					if (@string != null)
					{
						string string2 = json.GetString("title");
						if (string2 != null)
						{
							string string3 = json.GetString("html_url");
							if (string3 != null)
							{
								string string4 = json.GetString("state");
								if (string4 != null)
								{
									IssueState state;
									if (string4 == "open")
									{
										state = IssueState.Open;
									}
									else
									{
										if (!(string4 == "closed"))
										{
											Log.Error("Unknown pull request state '" + string4 + "'");
											return null;
										}
										state = IssueState.Closed;
									}
									string string5 = json.GetString("assignee", "login");
									string string6 = json.GetString("assignee", "avatar_url");
									return new Issue(@string, string2, string5, null, state, string6, string3);
								}
							}
						}
					}
				}
				Log.Warn("Cannot parse Issue");
				return null;
			}

			[Null]
			public static PullRequestsResponse DecodePullRequestsResponse(JObject json)
			{
				if (!(json["items"] is JArray jArray))
				{
					Log.Warn("Cannot parse 'items'");
					return null;
				}
				List<PullRequest> list = new List<PullRequest>(jArray.Count);
				foreach (JToken item in jArray)
				{
					PullRequest pullRequest = DecodePullRequest(item as JObject);
					if (pullRequest != null)
					{
						list.Add(pullRequest);
					}
				}
				return new PullRequestsResponse(list.ToArray());
			}

			[Null]
			private static PullRequest DecodePullRequest([Null] JObject json)
			{
				if (json != null)
				{
					string @string = json.GetString("number");
					if (@string != null)
					{
						string string2 = json.GetString("title");
						if (string2 != null)
						{
							string string3 = json.GetString("user", "login");
							if (string3 != null)
							{
								string string4 = json.GetString("html_url");
								if (string4 != null)
								{
									string string5 = json.GetString("state");
									if (string5 != null)
									{
										PullRequestState state;
										if (string5 == "open")
										{
											state = PullRequestState.Open;
										}
										else
										{
											if (!(string5 == "closed"))
											{
												Log.Error("Unknown pull request state '" + string5 + "'");
												return null;
											}
											state = PullRequestState.Closed;
										}
										string string6 = json.GetString("head", "ref");
										string string7 = json.GetString("user", "avatar_url");
										return new PullRequest(@string, string2, string6, string3, null, state, string7, string4);
									}
								}
							}
						}
					}
				}
				Log.Warn("Cannot parse Issue");
				return null;
			}

			[Null]
			public static string DecodeServiceError([Null] JObject json)
			{
				if (json != null)
				{
					string @string = json.GetString("message");
					if (@string != null)
					{
						Log.Warn(@string);
						return "GitHub Error: " + @string;
					}
				}
				Log.Warn("Cannot parse Error json");
				return null;
			}
		}

		private class IssuesResponse
		{
			public Issue[] Issues { get; }

			public IssuesResponse(Issue[] issues)
			{
				Issues = issues;
			}
		}

		private class PullRequestsResponse
		{
			public PullRequest[] PullRequests { get; }

			public PullRequestsResponse(PullRequest[] pullRequests)
			{
				PullRequests = pullRequests;
			}
		}

		private readonly bool _gitHubEnterprise;

		public override Func<string, string, SearchQuery.Parameter>[] AllowedQueryParameters { get; } = new Func<string, string, SearchQuery.Parameter>[3]
		{
			SearchQuery.Assignee.TryCreate,
			SearchQuery.Author.TryCreate,
			SearchQuery.Milestone.TryCreate
		};


		protected override int PageSize => 70;

		public GitHubService(Connection connection, bool gitHubEnterprise = false)
			: base(connection)
		{
			_gitHubEnterprise = gitHubEnterprise;
		}

		public override ServiceResult<User> GetUser()
		{
			return Request(GitHugSlug("/user"), Coder.DecodeUser);
		}

		public override IPaged<GitServiceRepository> GetRepositories()
		{
			return new Paginator<GitServiceRepository>(PageSize, delegate(int currentPage, int pageSize)
			{
				ApiRequest apiRequest = new ApiRequest(GitHugSlug("/user/repos"));
				apiRequest.AddParameter("page", currentPage);
				apiRequest.AddParameter("per_page", pageSize);
				return RequestArray(apiRequest, Coder.DecodeGitServiceRepositoryArray);
			});
		}

		public IPaged<GitServiceNotification> GetNotifications()
		{
			return new Paginator<GitServiceNotification>(PageSize, delegate(int currentPage, int pageSize)
			{
				ApiRequest apiRequest = new ApiRequest("/notifications");
				apiRequest.AddParameter("page", currentPage);
				apiRequest.AddParameter("per_page", pageSize);
				apiRequest.AddParameter("all", "true");
				return RequestArray(apiRequest, Coder.DecodeGitServiceNotificationArray);
			});
		}

		public override ServiceResult<string> GetNewIssueUrl(Remote remote)
		{
			string slug = remote.GitUrl.Slug;
			if (slug == null)
			{
				return ServiceResult<string>.Failure(new ServiceError.ParseError("Slug in '" + remote.Url + "'"));
			}
			return ServiceResult<string>.Success((_gitHubEnterprise ? Connection.ServerUrl : "https://github.com") + "/" + slug + "/issues/new");
		}

		public override IPaged<Issue> GetIssues(Remote remote, string queryString)
		{
			return new Paginator<Issue>(PageSize, delegate(int currentPage, int pageSize)
			{
				string slug = remote.GitUrl.Slug;
				if (slug == null)
				{
					return ServiceResult<Issue[]>.Failure(new ServiceError.ParseError("Slug in '" + remote.Url + "'"));
				}
				ApiRequest apiRequest = new ApiRequest(GitHugSlug("/search/issues"));
				SearchQuery query = SearchQueryParser.Parse(queryString, AllowedQueryParameters);
				ConfigureRequest(apiRequest, query, "repo:" + slug + " is:issue");
				apiRequest.AddParameter("page", currentPage);
				apiRequest.AddParameter("per_page", pageSize);
				return RequestArray(apiRequest, Coder.DecodeIssuesResponse, (IssuesResponse x) => x.Issues);
			});
		}

		public override ServiceResult<string> GetNewPullRequestUrl(Remote remote)
		{
			string slug = remote.GitUrl.Slug;
			if (slug == null)
			{
				return ServiceResult<string>.Failure(new ServiceError.ParseError("Slug in '" + remote.Url + "'"));
			}
			return ServiceResult<string>.Success((_gitHubEnterprise ? Connection.ServerUrl : "https://github.com") + "/" + slug + "/compare");
		}

		public override IPaged<PullRequest> GetPullRequests(Remote remote, string queryString)
		{
			return new Paginator<PullRequest>(PageSize, delegate(int currentPage, int pageSize)
			{
				string slug = remote.GitUrl.Slug;
				if (slug == null)
				{
					return ServiceResult<PullRequest[]>.Failure(new ServiceError.ParseError("Slug in '" + remote.Url + "'"));
				}
				ApiRequest apiRequest = new ApiRequest(GitHugSlug("/search/issues"));
				SearchQuery query = SearchQueryParser.Parse(queryString, AllowedQueryParameters);
				ConfigureRequest(apiRequest, query, "repo:" + slug + " is:pr");
				apiRequest.AddParameter("page", currentPage);
				apiRequest.AddParameter("per_page", pageSize);
				return RequestArray(apiRequest, Coder.DecodePullRequestsResponse, (PullRequestsResponse x) => x.PullRequests);
			});
		}

		protected override ServiceResult<T> DecodeJsonError<T>(ServiceError.RemoteServiceJsonError jsonError)
		{
			string text = Coder.DecodeServiceError(jsonError.Json as JObject);
			if (text != null)
			{
				return ServiceResult<T>.Failure(new ServiceError.RemoteServiceError(text));
			}
			return base.DecodeJsonError<T>(jsonError);
		}

		private string GitHugSlug(string slug)
		{
			if (_gitHubEnterprise)
			{
				return "/api/v3" + slug;
			}
			return slug;
		}

		private static void ConfigureRequest(ApiRequest request, SearchQuery query, string options)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(options + " sort:created-desc");
			if (query.Parameters.Length == 0)
			{
				stringBuilder.Append(" state:open");
				request.AddParameter("q", stringBuilder.ToString());
				return;
			}
			SearchQuery.Parameter[] parameters = query.Parameters;
			foreach (SearchQuery.Parameter parameter in parameters)
			{
				if (parameter is SearchQuery.Assignee assignee)
				{
					stringBuilder.Append(" assignee:" + assignee.Value);
				}
				else if (parameter is SearchQuery.Author author)
				{
					stringBuilder.Append(" author:" + author.Value);
				}
				else if (parameter is SearchQuery.Milestone milestone)
				{
					stringBuilder.Append(" milestone:" + milestone.Value);
				}
				else if (parameter is SearchQuery.SearchString searchString)
				{
					stringBuilder.Append(" ");
					stringBuilder.Append(searchString.Value);
				}
			}
			request.AddParameter("q", stringBuilder.ToString());
		}
	}
}
