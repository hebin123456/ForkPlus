using System;
using System.ComponentModel;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs.RepositoryOverview
{
	public class RepositoryOverviewCommitViewModel : INotifyPropertyChanged
	{
		public Revision Revision { get; }

		public UserIdentity Author => Revision.Author;

		public DateTime AuthorDate => Revision.AuthorDate;

		public string Subject => Revision.Message;

		public Sha Sha => Revision.Sha;

		public string FullCredentialsString => Revision.Author.Name + " <" + Revision.Author.Email + ">";

		public event PropertyChangedEventHandler PropertyChanged
		{
			add
			{
			}
			remove
			{
			}
		}

		public RepositoryOverviewCommitViewModel(Revision revision)
		{
			Revision = revision;
		}
	}
}
