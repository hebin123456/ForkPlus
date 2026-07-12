using System;
using System.Collections.Generic;
using System.Text;
using ForkPlus.Git;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Accounts
{
	public class BitbucketService : GitService
	{
		private class Coder
		{
			[Null]
			public static User DecodeUser(JObject json)
			{
				string @string = json.GetString("username");
				if (@string != null)
				{
					string string2 = json.GetString("display_name");
					if (string2 != null)
					{
						string string3 = json.GetString("links", "html", "href");
						if (string3 != null)
						{
							string string4 = json.GetString("links", "avatar", "href");
							if (string4 != null)
							{
								return new User(@string, string2, string4, string3);
							}
						}
					}
				}
				Log.Warn("Cannot parse User json");
				return null;
			}

			[Null]
			public static BitbucketWorkspaces DecodeBitbucketWorkspaces(JObject json)
			{
				if (!(json["values"] is JArray jArray))
				{
					Log.Warn("Cannot parse 'values'");
					return null;
				}
				List<BitbucketWorkspace> list = new List<BitbucketWorkspace>(jArray.Count);
				foreach (JToken item in jArray)
				{
					BitbucketWorkspace bitbucketWorkspace = DecodeBitbucketWorkspace(item as JObject);
					if (bitbucketWorkspace != null)
					{
						list.Add(bitbucketWorkspace);
					}
				}
				return new BitbucketWorkspaces(list.ToArray());
			}

			[Null]
			private static BitbucketWorkspace DecodeBitbucketWorkspace([Null] JObject json)
			{
				if (json == null)
				{
					return null;
				}
				if (!(json["workspace"] is JObject json2))
				{
					Log.Warn("Failed to find workspace in bitbucket workspace_access");
					return null;
				}
				string @string = json2.GetString("uuid");
				if (@string == null)
				{
					Log.Warn("Failed to find uuid in bitbucket workspace_base");
					return null;
				}
				string string2 = json2.GetString("slug");
				if (string2 == null)
				{
					Log.Warn("Failed to find slug in bitbucket workspace_base");
					return null;
				}
				string string3 = json2.GetString("type");
				if (string3 == null)
				{
					Log.Warn("Failed to find type in bitbucket workspace_base");
					return null;
				}
				return new BitbucketWorkspace(@string, string2, string3);
			}

			[Null]
			public static GitServiceRepositoriesResponse DecodeGitServiceRepositoriesResponse(JObject json)
			{
				int? @int = json.GetInt("size");
				if (@int.HasValue)
				{
					int valueOrDefault = @int.GetValueOrDefault();
					if (!(json["values"] is JArray jArray))
					{
						Log.Warn("Cannot parse 'values'");
						return null;
					}
					List<GitServiceRepository> list = new List<GitServiceRepository>(jArray.Count);
					foreach (JToken item in jArray)
					{
						GitServiceRepository gitServiceRepository = DecodeGitServiceRepository(item as JObject);
						if (gitServiceRepository != null)
						{
							list.Add(gitServiceRepository);
						}
					}
					return new GitServiceRepositoriesResponse(list.ToArray(), valueOrDefault);
				}
				Log.Warn("Cannot parse 'size'");
				return null;
			}

			[Null]
			private static GitServiceRepository DecodeGitServiceRepository([Null] JObject json)
			{
				if (json == null)
				{
					return null;
				}
				string @string = json.GetString("name");
				if (@string == null)
				{
					Log.Warn("Failed to find name in bitbucket repository");
					return null;
				}
				string string2 = json.GetString("owner", "display_name");
				if (string2 == null)
				{
					Log.Warn("Failed to find owner.display_name in bitbucket repository");
					return null;
				}
				string string3 = json.GetString("owner", "links", "avatar", "href");
				if (string3 == null)
				{
					Log.Warn("Failed to find owner.links.avatar in bitbucket repository");
					return null;
				}
				if (!(json["links"] is JObject jObject) || !(jObject["clone"] is JArray jArray))
				{
					Log.Warn("Cannot parse links.clone in bitbucket repository");
					return null;
				}
				string text = null;
				string text2 = null;
				foreach (JToken item in jArray)
				{
					if (item is JObject json2)
					{
						string string4 = json2.GetString("name");
						if (string4 == "https")
						{
							text = json2.GetString("href");
						}
						else if (string4 == "ssh")
						{
							text2 = json2.GetString("href");
						}
					}
				}
				if (text == null)
				{
					Log.Warn("Cannot parse name.ssh.href in GitServiceRepository");
					return null;
				}
				if (text2 == null)
				{
					Log.Warn("Cannot parse name.https.href in GitServiceRepository");
					return null;
				}
				return new GitServiceRepository(@string, string2, string3, text, text2);
			}

			[Null]
			public static PullRequestsResponse DecodePullRequestsResponse(JObject json)
			{
				if (!(json["values"] is JArray jArray))
				{
					Log.Warn("Cannot parse 'values'");
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
					string @string = json.GetString("id");
					if (@string != null)
					{
						string string2 = json.GetString("title");
						if (string2 != null)
						{
							string string3 = json.GetString("source", "branch", "name");
							if (string3 != null)
							{
								string string4 = json.GetString("author", "nickname");
								if (string4 != null)
								{
									string string5 = json.GetString("links", "html", "href");
									if (string5 != null)
									{
										string string6 = json.GetString("author", "links", "avatar", "href");
										if (string6 != null)
										{
											string string7 = json.GetString("state");
											if (string7 != null)
											{
												PullRequestState state;
												switch (string7)
												{
												case "OPEN":
													state = PullRequestState.Open;
													break;
												case "DECLINED":
													state = PullRequestState.Closed;
													break;
												case "MERGED":
													state = PullRequestState.Merged;
													break;
												case "locked":
													state = PullRequestState.Closed;
													break;
												default:
													Log.Error("Unknown pull request state '" + string7 + "'");
													return null;
												}
												string string8 = json.GetString("author", "display_name");
												return new PullRequest(@string, string2, string3, string4, string8, state, string6, string5);
											}
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
				if (json != null)
				{
					string @string = json.GetString("error", "message");
					if (@string != null)
					{
						if (@string.Contains("lack one or more required privilege scopes"))
						{
							return PreferencesLocalization.Current("Access to Bitbucket repositories requires 'read:workspace:bitbucket' scope");
						}
						Log.Warn(@string);
						return PreferencesLocalization.FormatCurrent("Bitbucket Error: {0}", @string);
					}
				}
				Log.Warn("Cannot parse Error json");
				return null;
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

		private class GitServiceRepositoriesResponse
		{
			public GitServiceRepository[] Repositories { get; }

			public int TotalItems { get; }

			public GitServiceRepositoriesResponse(GitServiceRepository[] repositories, int totalItems)
			{
				Repositories = repositories;
				TotalItems = totalItems;
			}
		}

		private class BitbucketWorkspaces
		{
			public BitbucketWorkspace[] Values { get; }

			public BitbucketWorkspaces(BitbucketWorkspace[] values)
			{
				Values = values;
			}
		}

		private class BitbucketWorkspace
		{
			public string Uuid { get; }

			public string Slug { get; }

			public string Type { get; }

			public BitbucketWorkspace(string uuid, string slug, string type)
			{
				Uuid = uuid;
				Slug = slug;
				Type = type;
			}
		}

		public override Func<string, string, SearchQuery.Parameter>[] AllowedQueryParameters { get; } = new Func<string, string, SearchQuery.Parameter>[2]
		{
			SearchQuery.Author.TryCreate,
			SearchQuery.Assignee.TryCreate
		};


		public override bool SupportsIssues => false;

		public BitbucketService(Connection connection)
			: base(connection)
		{
		}

		public override ServiceResult<User> GetUser()
		{
			return Request("/2.0/user", Coder.DecodeUser);
		}

		public override IPaged<GitServiceRepository> GetRepositories()
		{
			return new Paginator<GitServiceRepository>(10000, delegate
			{
				ApiRequest request = new ApiRequest("/2.0/user/workspaces");
				ServiceResult<BitbucketWorkspace[]> serviceResult = RequestArray(request, Coder.DecodeBitbucketWorkspaces, (BitbucketWorkspaces x) => x.Values);
				if (serviceResult.Error != null)
				{
					return ServiceResult<GitServiceRepository[]>.Failure(serviceResult.Error);
				}
				List<GitServiceRepository> list = new List<GitServiceRepository>();
				BitbucketWorkspace[] result = serviceResult.Result;
				foreach (BitbucketWorkspace bitbucketWorkspace in result)
				{
					int num = 100;
					int j = 0;
					int totalItems;
					for (totalItems = 0; j < totalItems / num + 1; j++)
					{
						ApiRequest apiRequest = new ApiRequest("/2.0/repositories/" + bitbucketWorkspace.Slug);
						apiRequest.AddParameter("page", j + 1);
						apiRequest.AddParameter("pagelen", num);
						ServiceResult<GitServiceRepository[]> serviceResult2 = RequestArray(apiRequest, Coder.DecodeGitServiceRepositoriesResponse, delegate(GitServiceRepositoriesResponse x)
						{
							totalItems = x.TotalItems;
							return x.Repositories;
						});
						if (serviceResult2.Error != null)
						{
							return ServiceResult<GitServiceRepository[]>.Failure(serviceResult2.Error);
						}
						list.AddRange(serviceResult2.Result);
					}
				}
				list.Sort((GitServiceRepository x, GitServiceRepository y) => x.Name.CompareTo(y.Name));
				return ServiceResult<GitServiceRepository[]>.Success(list.ToArray());
			});
		}

		public override IPaged<Issue> GetIssues(Remote remote, string queryString)
		{
			return new Paginator<Issue>(PageSize, delegate
			{
				throw new NotSupportedException();
			});
		}

		public override ServiceResult<string> GetNewIssueUrl(Remote remote)
		{
			throw new NotSupportedException();
		}

		public override ServiceResult<string> GetNewPullRequestUrl(Remote remote)
		{
			string slug = remote.GitUrl.Slug;
			if (slug == null)
			{
				return ServiceResult<string>.Failure(new ServiceError.ParseError(PreferencesLocalization.FormatCurrent("Slug in '{0}'", remote.Url)));
			}
			return ServiceResult<string>.Success("https://bitbucket.org/" + slug + "/pull-requests/new");
		}

		public override IPaged<PullRequest> GetPullRequests(Remote remote, string queryString)
		{
			return new Paginator<PullRequest>(PageSize, delegate(int currentPage, int pageSize)
			{
				string slug = remote.GitUrl.Slug;
				if (slug == null)
				{
					return ServiceResult<PullRequest[]>.Failure(new ServiceError.ParseError(PreferencesLocalization.FormatCurrent("Slug in '{0}'", remote.Url)));
				}
				ApiRequest apiRequest = new ApiRequest("/2.0/repositories", slug, "pullrequests");
				SearchQuery query = SearchQueryParser.Parse(queryString, AllowedQueryParameters);
				ConfigureRequest(apiRequest, query);
				apiRequest.AddParameter("page", currentPage);
				apiRequest.AddParameter("pagelen", pageSize);
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

		private static void ConfigureRequest(ApiRequest request, SearchQuery query)
		{
			StringBuilder stringBuilder = new StringBuilder();
			if (query.Parameters.Length == 0)
			{
				stringBuilder.Append("state=\"OPEN\"");
				request.AddParameter("q", stringBuilder.ToString());
				return;
			}
			stringBuilder.Append("(state=\"MERGED\" OR state=\"DECLINED\" OR state=\"OPEN\")");
			SearchQuery.Parameter[] parameters = query.Parameters;
			foreach (SearchQuery.Parameter parameter in parameters)
			{
				if (parameter is SearchQuery.Assignee assignee)
				{
					stringBuilder.Append(" AND reviewers.nickname=\"" + assignee.Value + "\"");
				}
				else if (parameter is SearchQuery.Author author)
				{
					stringBuilder.Append(" AND author.username=\"" + author.Value + "\"");
				}
				else if (parameter is SearchQuery.SearchString searchString)
				{
					stringBuilder.Append(" AND title ~ \"" + searchString.Value + "\"");
				}
			}
			request.AddParameter("q", stringBuilder.ToString());
		}
	}
}
