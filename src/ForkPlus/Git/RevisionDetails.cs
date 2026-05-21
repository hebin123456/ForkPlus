using System;

namespace ForkPlus.Git
{
	public class RevisionDetails : Revision, IGitPoint
	{
		public UserIdentity Committer { get; }

		public DateTime CommitterDate { get; }

		public Sha[] Parents { get; }

		string IGitPoint.FriendlyName
		{
			get
			{
				MessageParts(out var subject, out var _);
				return subject;
			}
		}

		public RevisionDetails(Sha sha, UserIdentity author, DateTime authorDate, UserIdentity committer, DateTime committerDate, Sha[] parents, string message)
			: base(sha, new RevisionHeader(author, authorDate, message, hasBody: false))
		{
			Committer = committer;
			CommitterDate = committerDate;
			Parents = parents;
		}
	}
}
