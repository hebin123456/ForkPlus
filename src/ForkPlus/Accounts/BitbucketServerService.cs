using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using ForkPlus.Git;
using ForkPlus.Utils.Http;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Accounts
{
	public class BitbucketServerService : GitService
	{
		private class Coder
		{
			[Null]
			public static User DecodeUser(JObject json, string baseUrl)
			{
				string @string = json.GetString("name");
				if (@string != null)
				{
					string string2 = json.GetString("displayName");
					if (string2 != null)
					{
						string string3 = json.GetString("links", "self", 0, "href");
						if (string3 != null)
						{
							string avatarUrl = baseUrl + "/users/" + @string + "/avatar.png";
							return new User(@string, string2, avatarUrl, string3);
						}
					}
				}
				Log.Warn("Cannot parse User json");
				return null;
			}

			[Null]
			public static GitServiceRepositoriesResponse DecodeGitServiceRepositoriesResponse(JObject json, string baseUrl)
			{
				if (json["values"] is JArray jArray)
				{
					bool? flag = json["isLastPage"]?.Value<bool>();
					if (flag.HasValue)
					{
						bool valueOrDefault = flag.GetValueOrDefault();
						int nextPageStart = json["nextPageStart"]?.Value<int>() ?? (-1);
						List<GitServiceRepository> list = new List<GitServiceRepository>(jArray.Count);
						foreach (JToken item in jArray)
						{
							GitServiceRepository gitServiceRepository = DecodeGitServiceRepository(item as JObject, baseUrl);
							if (gitServiceRepository != null)
							{
								list.Add(gitServiceRepository);
							}
						}
						return new GitServiceRepositoriesResponse(list.ToArray(), valueOrDefault, nextPageStart);
					}
				}
				Log.Warn("Cannot parse 'values'");
				return null;
			}

			[Null]
			private static GitServiceRepository DecodeGitServiceRepository([Null] JObject json, string baseUrl)
			{
				if (json != null)
				{
					string @string = json.GetString("name");
					if (@string != null && json["links"] is JObject jObject && jObject["clone"] is JArray jArray)
					{
						string text = null;
						string text2 = null;
						foreach (JToken item in jArray)
						{
							if (item is JObject json2)
							{
								string string2 = json2.GetString("name");
								if (string2 == "http")
								{
									text = json2.GetString("href");
								}
								else if (string2 == "ssh")
								{
									text2 = json2.GetString("href");
								}
							}
						}
						if (text == null || text2 == null)
						{
							Log.Warn("Cannot parse clone urls in GitServiceRepository");
							return null;
						}
						string text3 = json.GetString("project", "key");
						string ownerAvatarUrl;
						if (text3.StartsWith("~"))
						{
							text3 = TrimStart(text3, "~");
							ownerAvatarUrl = baseUrl + "/users/" + text3 + "/avatar.png";
						}
						else
						{
							ownerAvatarUrl = baseUrl + "/projects/" + text3 + "/avatar.png";
						}
						return new GitServiceRepository(@string, text3, ownerAvatarUrl, text, text2);
					}
				}
				Log.Warn("Cannot parse GitServiceRepository");
				return null;
			}

			[Null]
			public static PullRequestsResponse DecodePullRequestsResponse(JObject json, string baseUrl)
			{
				if (json["values"] is JArray jArray)
				{
					bool? flag = json["isLastPage"]?.Value<bool>();
					if (flag.HasValue)
					{
						bool valueOrDefault = flag.GetValueOrDefault();
						int nextPageStart = json["nextPageStart"]?.Value<int>() ?? (-1);
						List<PullRequest> list = new List<PullRequest>(jArray.Count);
						foreach (JToken item in jArray)
						{
							PullRequest pullRequest = DecodePullRequest(item as JObject, baseUrl);
							if (pullRequest != null)
							{
								list.Add(pullRequest);
							}
						}
						return new PullRequestsResponse(list.ToArray(), valueOrDefault, nextPageStart);
					}
				}
				Log.Warn("Cannot parse 'values'");
				return null;
			}

			[Null]
			private static PullRequest DecodePullRequest([Null] JObject json, string baseUrl)
			{
				if (json != null)
				{
					string @string = json.GetString("id");
					if (@string != null)
					{
						string string2 = json.GetString("title");
						if (string2 != null)
						{
							string string3 = json.GetString("fromRef", "id");
							if (string3 != null)
							{
								string string4 = json.GetString("author", "user", "name");
								if (string4 != null)
								{
									string string5 = json.GetString("links", "self", 0, "href");
									if (string5 != null)
									{
										string string6 = json.GetString("state");
										if (string6 != null)
										{
											PullRequestState state;
											switch (string6)
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
												Log.Error("Unknown pull request state '" + string6 + "'");
												return null;
											}
											string sourceBranch = string3.Replace("refs/heads/", "");
											string string7 = json.GetString("author", "user", "displayName");
											string authorAvatarUrl = baseUrl + "/users/" + string4 + "/avatar.png";
											return new PullRequest(@string, string2, sourceBranch, string4, string7, state, authorAvatarUrl, string5);
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
					string @string = json.GetString("errors", 0, "message");
					if (@string != null)
					{
						Log.Warn(@string);
						return "BitBucket Error: " + @string;
					}
				}
				Log.Warn("Cannot parse Error json");
				return null;
			}
		}

		private class PullRequestsResponse
		{
			public PullRequest[] PullRequests { get; }

			public bool IsLastPage { get; }

			public int NextPageStart { get; }

			public PullRequestsResponse(PullRequest[] pullRequests, bool isLastPage, int nextPageStart)
			{
				PullRequests = pullRequests;
				IsLastPage = isLastPage;
				NextPageStart = nextPageStart;
			}
		}

		private class GitServiceRepositoriesResponse
		{
			public GitServiceRepository[] Repositories { get; }

			public bool IsLastPage { get; }

			public int NextPageStart { get; }

			public GitServiceRepositoriesResponse(GitServiceRepository[] repositories, bool isLastPage, int nextPageStart)
			{
				Repositories = repositories;
				IsLastPage = isLastPage;
				NextPageStart = nextPageStart;
			}
		}

		private class BitbucketServerPaginator<T> : IPaged<T>
		{
			private readonly int _pageSize;

			private object _lock = new object();

			private bool _hasNext = true;

			private readonly Func<BitbucketServerPaginator<T>, int, int, ServiceResult<T[]>> _request;

			public int NextPageStart { get; set; }

			public bool HasNext
			{
				get
				{
					lock (_lock)
					{
						return _hasNext;
					}
				}
				set
				{
					lock (_lock)
					{
						_hasNext = value;
					}
				}
			}

			public BitbucketServerPaginator(int pageSize, Func<BitbucketServerPaginator<T>, int, int, ServiceResult<T[]>> request)
			{
				_pageSize = pageSize;
				_request = request;
			}

			public ServiceResult<T[]> LoadNext()
			{
				if (!HasNext)
				{
					return ServiceResult<T[]>.Failure(new ServiceError.EmptyPaginatorError());
				}
				return _request(this, NextPageStart, _pageSize);
			}
		}

		public override bool SupportsIssues => false;

		public BitbucketServerService(Connection connection)
			: base(connection)
		{
		}

		public override ServiceResult<User> GetUser()
		{
			Connection.HttpRequestResult httpRequestResult = Connection.Request(new ApiRequest("/rest/api/1.0/application-properties"));
			if (!httpRequestResult.Succeeded)
			{
				return ServiceResult<User>.Failure(httpRequestResult.Error);
			}
			string usernameHeader = GetUsernameHeader(httpRequestResult);
			if (usernameHeader == null)
			{
				return ServiceResult<User>.Failure(new ServiceError.UnknownError("Token is not valid"));
			}
			usernameHeader = Uri.UnescapeDataString(usernameHeader);
			usernameHeader = usernameHeader.Replace('@', '_');
			return Request("/rest/api/1.0/users/" + usernameHeader, (JObject jObject) => Coder.DecodeUser(jObject, Connection.ServerUrl));
		}

		public override IPaged<GitServiceRepository> GetRepositories()
		{
			return new BitbucketServerPaginator<GitServiceRepository>(PageSize, delegate(BitbucketServerPaginator<GitServiceRepository> paginator, int start, int pageSize)
			{
				ApiRequest apiRequest = new ApiRequest("/rest/api/1.0/repos");
				apiRequest.AddParameter("start", start);
				apiRequest.AddParameter("limit", pageSize);
				return RequestArray(apiRequest, (JObject jObject) => Coder.DecodeGitServiceRepositoriesResponse(jObject, Connection.ServerUrl), delegate(GitServiceRepositoriesResponse x)
				{
					paginator.HasNext = !x.IsLastPage;
					paginator.NextPageStart = x.NextPageStart;
					return x.Repositories;
				});
			});
		}

		[Null]
		private static string GetUsernameHeader(Connection.HttpRequestResult response)
		{
			IEnumerable<string> enumerable = default(IEnumerable<string>);
			if (((HttpHeaders)response.Headers).TryGetValues("X-AUSERNAME", out enumerable))
			{
				using IEnumerator<string> enumerator = enumerable.GetEnumerator();
				if (enumerator.MoveNext())
				{
					return enumerator.Current;
				}
			}
			IEnumerable<string> enumerable2 = default(IEnumerable<string>);
			if (((HttpHeaders)response.Headers).TryGetValues("x-ausername", out enumerable2))
			{
				using IEnumerator<string> enumerator = enumerable2.GetEnumerator();
				if (enumerator.MoveNext())
				{
					return enumerator.Current;
				}
			}
			IEnumerable<string> enumerable3 = default(IEnumerable<string>);
			if (((HttpHeaders)response.Headers).TryGetValues("X-ausername", out enumerable3))
			{
				using IEnumerator<string> enumerator = enumerable3.GetEnumerator();
				if (enumerator.MoveNext())
				{
					return enumerator.Current;
				}
			}
			return null;
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
				return ServiceResult<string>.Failure(new ServiceError.ParseError("Remote url '" + remote.Url + "'"));
			}
			string text = CreateBitbucketServerSlug(slug);
			if (text == null)
			{
				return ServiceResult<string>.Failure(new ServiceError.ParseError("Cannot create slug for '" + slug + "'"));
			}
			return ServiceResult<string>.Success(Connection.ServerUrl + "/" + text + "/pull-requests?create");
		}

		public override IPaged<PullRequest> GetPullRequests(Remote remote, string queryString)
		{
			return new BitbucketServerPaginator<PullRequest>(PageSize, delegate(BitbucketServerPaginator<PullRequest> paginator, int start, int pageSize)
			{
				string slug = remote.GitUrl.Slug;
				if (slug == null)
				{
					return ServiceResult<PullRequest[]>.Failure(new ServiceError.ParseError("Slug in '" + remote.Url + "'"));
				}
				string text = CreateBitbucketServerSlug(slug);
				if (text == null)
				{
					return ServiceResult<PullRequest[]>.Failure(new ServiceError.ParseError("Cannot create slug for '" + slug + "'"));
				}
				ApiRequest apiRequest = new ApiRequest("/rest/api/1.0", text, "pull-requests");
				SearchQuery query = SearchQueryParser.Parse(queryString, AllowedQueryParameters);
				ConfigureRequest(apiRequest, query);
				apiRequest.AddParameter("start", start);
				apiRequest.AddParameter("limit", pageSize);
				return RequestArray(apiRequest, (JObject jObject) => Coder.DecodePullRequestsResponse(jObject, Connection.ServerUrl), delegate(PullRequestsResponse x)
				{
					paginator.HasNext = !x.IsLastPage;
					paginator.NextPageStart = x.NextPageStart;
					return x.PullRequests;
				});
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
				request.AddParameter("state", "open");
				return;
			}
			SearchQuery.Parameter[] parameters = query.Parameters;
			for (int i = 0; i < parameters.Length; i++)
			{
				if (parameters[i] is SearchQuery.SearchString searchString)
				{
					request.AddParameter("filterText", searchString.Value);
				}
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

		private static string TrimStart(string target, string trimString)
		{
			if (target.StartsWith(trimString))
			{
				return target.Substring(trimString.Length);
			}
			return target;
		}
	}
}
