using System;

namespace ForkPlus.Git.Commands
{
	public class InteractiveRebaseTodoListItem
	{
		public Sha Sha { get; }

		public InteractiveRebaseAction Action { get; }

		public UserIdentity Author { get; }

		public DateTime AuthorDate { get; }

		public string Message { get; }

		public LocalBranch[] Refs { get; set; }

		public InteractiveRebaseTodoListItem(Sha sha, InteractiveRebaseAction action, UserIdentity author, DateTime authorDate, string message, LocalBranch[] refs)
		{
			Sha = sha;
			Action = action;
			Author = author;
			AuthorDate = authorDate;
			Message = message;
			Refs = refs;
		}
	}
}
