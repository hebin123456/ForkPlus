using System;

namespace ForkPlus.Git.Commands
{
	public class RevisionWithFiles
	{
		public Revision Revision { get; }

		public Sha Sha => Revision.Sha;

		public UserIdentity Author => Revision.Author;

		public DateTime AuthorDate => Revision.AuthorDate;

		public string Message => Revision.Message;

		public ChangedFile[] ChangedFiles { get; }

		public RevisionWithFiles(Revision revision, ChangedFile[] changedFiles)
		{
			Revision = revision;
			ChangedFiles = changedFiles;
		}
	}
}
