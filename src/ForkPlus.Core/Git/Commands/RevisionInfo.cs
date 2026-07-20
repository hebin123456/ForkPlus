using System;

namespace ForkPlus.Git.Commands
{
	public struct RevisionInfo
	{
		public Sha Sha { get; }

		public UserIdentity Author { get; }

		public DateTime AuthorDate { get; }

		public string Message { get; }

		public RevisionInfo(Sha sha, UserIdentity author, DateTime authorDate, string message)
		{
			Sha = sha;
			Author = author;
			AuthorDate = authorDate;
			Message = message;
		}
	}
}
