using System;
using System.ComponentModel;
using ForkPlus.Git;
using ForkPlus.Git.Commands;

namespace ForkPlus.UI.Dialogs
{
	public class RevisionViewModel : INotifyPropertyChanged
	{
		public readonly RevisionWithFiles Revision;

		public DateTime AuthorDate => Revision.AuthorDate;

		public Sha Sha => Revision.Sha;

		public string AbbreviatedSha => Revision.Sha.ToAbbreviatedString();

		public string RevisionSubject => Revision.Message;

		public UserIdentity Author => Revision.Author;

		public string FilePath { get; }

		public ChangedFile ChangedFile => Revision.ChangedFiles.FirstItem();

		public event PropertyChangedEventHandler PropertyChanged
		{
			add
			{
			}
			remove
			{
			}
		}

		public RevisionViewModel(RevisionWithFiles revision)
		{
			Revision = revision;
			FilePath = revision.ChangedFiles.FirstItem().Path;
		}
	}
}
