using System;
using System.ComponentModel;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	public class BlameItemViewModel : INotifyPropertyChanged
	{
		public readonly Revision Revision;

		public UserIdentity Author => Revision.Author;

		public DateTime AuthorDate => Revision.AuthorDate;

		public string AbbreviatedSha => Revision.Sha.ToString().Substring(0, 6);

		public Sha RevisionSha => Revision.Sha;

		public string RevisionSubject => Revision.Message;

		public string FullCredentials => Revision.Author.Name + " <" + Revision.Author.Email + ">";

		public string ShaToolTip => "Navigate to '" + Revision.Sha.ToAbbreviatedString() + "'";

		public string OpenInSeparateWindowButtonToolTip => "Open '" + Revision.Sha.ToAbbreviatedString() + "' in separate window";

		public event PropertyChangedEventHandler PropertyChanged
		{
			add
			{
			}
			remove
			{
			}
		}

		public BlameItemViewModel(Revision revision)
		{
			Revision = revision;
		}
	}
}
