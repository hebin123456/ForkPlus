using System;
using ForkPlus.Git;
using ForkPlus.Utils.Http;

namespace ForkPlus.Accounts
{
	public abstract class GitService : RestClientBase
	{
		protected virtual int PageSize => 30;

		public virtual Func<string, string, SearchQuery.Parameter>[] AllowedQueryParameters { get; } = new Func<string, string, SearchQuery.Parameter>[0];


		public virtual bool SupportsIssues => true;

		public GitService(Connection connection)
			: base(connection)
		{
		}

		public abstract ServiceResult<User> GetUser();

		public abstract IPaged<GitServiceRepository> GetRepositories();

		public abstract ServiceResult<string> GetNewIssueUrl(Remote remote);

		public abstract IPaged<Issue> GetIssues(Remote remote, string queryString);

		public abstract ServiceResult<string> GetNewPullRequestUrl(Remote remote);

		public abstract IPaged<PullRequest> GetPullRequests(Remote remote, string queryString);
	}
}
