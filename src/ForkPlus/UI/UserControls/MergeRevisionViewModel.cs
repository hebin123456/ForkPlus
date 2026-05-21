using System;
using System.ComponentModel;
using ForkPlus.Git;

namespace ForkPlus.UI.UserControls
{
	public class MergeRevisionViewModel : INotifyPropertyChanged
	{
		private readonly Revision _revision;

		public UserIdentity Author => _revision.Author;

		public DateTime AuthorDate => _revision.AuthorDate;

		public Sha Sha => _revision.Sha;

		public string Subject => _revision.Message;

		public string AbbreviatedShaString => _revision.Sha.ToString().Substring(0, 6);

		public string FullCredentialsString => _revision.Author.Name + " <" + _revision.Author.Email + ">";

		public event PropertyChangedEventHandler PropertyChanged
		{
			add
			{
			}
			remove
			{
			}
		}

		public MergeRevisionViewModel(Revision revision)
		{
			_revision = revision;
		}
	}
}
