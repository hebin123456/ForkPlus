using System;
using System.Collections.Generic;
using System.Net;
using ForkPlus.Git;
using ForkPlus.Utils.Http;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Accounts
{
	public class GitLabService : GitService
	{
		private class Coder
		{
			[Null]
			public static User DecodeUser(JObject json)
			{
				string @string = json.GetString("username");
				if (@string == null)
				{
					Log.Warn("Cannot parse User json");
					return null;
				}
				string string2 = json.GetString("name");
				string string3 = json.GetString("avatar_url");
				string string4 = json.GetString("web_url");
				return new User(@string, string2, string3, string4);
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
						string string2 = json.GetString("http_url_to_repo");
						if (string2 != null)
						{
							string string3 = json.GetString("ssh_url_to_repo");
							if (string3 != null)
							{
								string owner = json.GetString("namespace", "name") ?? json.GetString("owner", "name") ?? "";
								string ownerAvatarUrl = json.GetString("avatar_url") ?? json.GetString("namespace", "avatar_url") ?? json.GetString("owner", "avatar_url");
								return new GitServiceRepository(@string, owner, ownerAvatarUrl, string2, string3);
							}
						}
					}
				}
				Log.Warn("Cannot parse GitServiceRepository");
				return null;
			}

			[Null]
			public static Issue[] DecodeIssueArray(JArray jArray)
			{
				List<Issue> list = new List<Issue>(jArray.Count);
				foreach (JToken item in jArray)
				{
					Issue issue = DecodeIssue(item as JObject);
					if (issue != null)
					{
						list.Add(issue);
						continue;
					}
					return null;
				}
				return list.ToArray();
			}

			[Null]
			private static Issue DecodeIssue([Null] JObject json)
			{
				if (json != null)
				{
					string @string = json.GetString("iid");
					if (@string != null)
					{
						string string2 = json.GetString("title");
						if (string2 != null)
						{
							string string3 = json.GetString("web_url");
							if (string3 != null)
							{
								IssueState? issueState = DecodeIssueState(json["state"]);
								if (issueState.HasValue)
								{
									IssueState valueOrDefault = issueState.GetValueOrDefault();
									string string4 = json.GetString("assignee", "username");
									string string5 = json.GetString("assignee", "name");
									string string6 = json.GetString("assignee", "avatar_url");
									return new Issue(@string, string2, string4, string5, valueOrDefault, string6, string3);
								}
							}
						}
					}
				}
				Log.Warn("Cannot parse Issue");
				return null;
			}

			[Null]
			private static IssueState? DecodeIssueState(JToken jToken)
			{
				string text = jToken?.Value<string>();
				if (text == "opened")
				{
					return IssueState.Open;
				}
				if (text == "closed")
				{
					return IssueState.Closed;
				}
				Log.Warn("Cannot parse IssueState in '" + text + "'");
				return null;
			}

			public static PullRequest[] DecodePullRequestArray(JArray jArray)
			{
				List<PullRequest> list = new List<PullRequest>(jArray.Count);
				foreach (JToken item in jArray)
				{
					PullRequest pullRequest = DecodePullRequest(item as JObject);
					if (pullRequest != null)
					{
						list.Add(pullRequest);
					}
				}
				return list.ToArray();
			}

			[Null]
			private static PullRequest DecodePullRequest([Null] JObject json)
			{
				if (json != null)
				{
					string @string = json.GetString("iid");
					if (@string != null)
					{
						string string2 = json.GetString("title");
						if (string2 != null)
						{
							string string3 = json.GetString("source_branch");
							if (string3 != null)
							{
								string string4 = json.GetString("author", "username");
								if (string4 != null)
								{
									string string5 = json.GetString("web_url");
									if (string5 != null)
									{
										string string6 = json.GetString("state");
										if (string6 != null)
										{
											PullRequestState state;
											switch (string6)
											{
											case "opened":
												state = PullRequestState.Open;
												break;
											case "closed":
												state = PullRequestState.Closed;
												break;
											case "merged":
												state = PullRequestState.Merged;
												break;
											case "locked":
												state = PullRequestState.Closed;
												break;
											default:
												Log.Error("Unknown pull request state '" + string6 + "'");
												return null;
											}
											string string7 = json.GetString("author", "name");
											string string8 = json.GetString("author", "avatar_url");
											return new PullRequest(@string, string2, string3, string4, string7, state, string8, string5);
										}
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
				if (json == null)
				{
					Log.Warn("Cannot parse Error json");
					return null;
				}
				string @string = json.GetString("error");
				if (@string != null)
				{
					string string2 = json.GetString("error_description");
					Log.Warn(@string + ": " + string2);
					return "GitLab error: " + (string2 ?? @string);
				}
				string string3 = json.GetString("message");
				if (string3 != null)
				{
					Log.Warn(string3);
					return "GitLab error: " + string3;
				}
				return null;
			}
		}

		private readonly bool _gitlabServer;

		public override Func<string, string, SearchQuery.Parameter>[] AllowedQueryParameters { get; } = new Func<string, string, SearchQuery.Parameter>[3]
		{
			SearchQuery.Assignee.TryCreate,
			SearchQuery.Author.TryCreate,
			SearchQuery.Milestone.TryCreate
		};


		public GitLabService(Connection connection, bool gitlabServer = false)
			: base(connection)
		{
			_gitlabServer = gitlabServer;
		}

		public override ServiceResult<User> GetUser()
		{
			return Request("/api/v4/user", Coder.DecodeUser);
		}

		public override IPaged<GitServiceRepository> GetRepositories()
		{
			return new Paginator<GitServiceRepository>(PageSize, delegate(int currentPage, int pageSize)
			{
				ApiRequest apiRequest = new ApiRequest("/api/v4/projects");
				apiRequest.AddParameter("page", currentPage);
				apiRequest.AddParameter("per_page", pageSize);
				if (!_gitlabServer)
				{
					apiRequest.AddParameter("membership", "true");
				}
				return RequestArray(apiRequest, Coder.DecodeGitServiceRepositoryArray);
			});
		}

		public override ServiceResult<string> GetNewIssueUrl(Remote remote)
		{
			string slug = remote.GitUrl.Slug;
			if (slug == null)
			{
				return ServiceResult<string>.Failure(new ServiceError.ParseError("Slug in '" + remote.Url + "'"));
			}
			return ServiceResult<string>.Success(Connection.ServerUrl + "/" + slug + "/issues/new");
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
				ApiRequest apiRequest = new ApiRequest("/api/v4/projects", WebUtility.UrlEncode(slug), "issues");
				apiRequest.AddParameter("scope", "all");
				SearchQuery query = SearchQueryParser.Parse(queryString, AllowedQueryParameters);
				ConfigureRequest(apiRequest, query);
				apiRequest.AddParameter("page", currentPage);
				apiRequest.AddParameter("per_page", pageSize);
				return RequestArray(apiRequest, Coder.DecodeIssueArray);
			});
		}

		public override ServiceResult<string> GetNewPullRequestUrl(Remote remote)
		{
			string slug = remote.GitUrl.Slug;
			if (slug == null)
			{
				return ServiceResult<string>.Failure(new ServiceError.ParseError("Slug in '" + remote.Url + "'"));
			}
			return ServiceResult<string>.Success(Connection.ServerUrl + "/" + slug + "/merge_requests/new");
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
				ApiRequest apiRequest = new ApiRequest("/api/v4/projects", WebUtility.UrlEncode(slug), "merge_requests");
				apiRequest.AddParameter("scope", "all");
				SearchQuery query = SearchQueryParser.Parse(queryString, AllowedQueryParameters);
				ConfigureRequest(apiRequest, query);
				apiRequest.AddParameter("page", currentPage);
				apiRequest.AddParameter("per_page", pageSize);
				return RequestArray(apiRequest, Coder.DecodePullRequestArray);
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

		private static void ConfigureRequest(ApiRequest request, SearchQuery query)
		{
			if (query.Parameters.Length == 0)
			{
				request.AddParameter("state", "opened");
				return;
			}
			SearchQuery.Parameter[] parameters = query.Parameters;
			foreach (SearchQuery.Parameter parameter in parameters)
			{
				if (parameter is SearchQuery.Author author)
				{
					request.AddParameter("author_username", author.Value);
				}
				else if (parameter is SearchQuery.Assignee assignee)
				{
					request.AddParameter("assignee_username", assignee.Value);
				}
				else if (parameter is SearchQuery.Milestone milestone)
				{
					request.AddParameter("milestone", milestone.Value);
				}
				else if (parameter is SearchQuery.SearchString searchString)
				{
					request.AddParameter("search", searchString.Value);
					request.AddParameter("in", "title,description");
				}
			}
		}
	}
}
