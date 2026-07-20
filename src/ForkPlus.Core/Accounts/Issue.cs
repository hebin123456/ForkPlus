namespace ForkPlus.Accounts
{
	public class Issue
	{
		public string Id { get; }

		public string Title { get; }

		[Null]
		public string AssigneeUsername { get; }

		[Null]
		public string AssigneeName { get; }

		public IssueState State { get; }

		[Null]
		public string AssigneeAvatarUrl { get; }

		public string WebUrl { get; }

		public Issue(string id, string title, [Null] string assigneeUsername, [Null] string assigneeName, IssueState state, [Null] string assigneeAvatarUrl, string webUrl)
		{
			Id = id;
			Title = title;
			AssigneeUsername = assigneeUsername;
			AssigneeName = assigneeName;
			State = state;
			AssigneeAvatarUrl = assigneeAvatarUrl;
			WebUrl = webUrl;
		}
	}
}
