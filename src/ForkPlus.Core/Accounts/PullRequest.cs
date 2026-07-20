namespace ForkPlus.Accounts
{
	public class PullRequest
	{
		public string Id { get; }

		public string Title { get; }

		[Null]
		public string SourceBranch { get; }

		public string AuthorUsername { get; }

		[Null]
		public string AuthorName { get; }

		public PullRequestState State { get; }

		[Null]
		public string AuthorAvatarUrl { get; }

		public string WebUrl { get; }

		public PullRequest(string id, string title, [Null] string sourceBranch, string authorUsername, [Null] string authorName, PullRequestState state, [Null] string authorAvatarUrl, string webUrl)
		{
			Id = id;
			Title = title;
			SourceBranch = sourceBranch;
			AuthorUsername = authorUsername;
			AuthorName = authorName;
			State = state;
			AuthorAvatarUrl = authorAvatarUrl;
			WebUrl = webUrl;
		}
	}
}
