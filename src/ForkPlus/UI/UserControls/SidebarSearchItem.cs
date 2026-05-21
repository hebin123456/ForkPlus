using System;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.UserControls
{
	public class SidebarSearchItem : MultiselectionTreeViewItem
	{
		protected RevisionWithFiles Revision { get; }

		public Sha Sha => Revision.Sha;

		public DateTime AuthorDate => Revision.AuthorDate;

		public UserIdentity Author => Revision.Author;

		public bool ShowBorder { get; }

		public string SearchString { get; }

		public SidebarSearchItem(RevisionWithFiles revision, [Null] string searchString, bool initializeChangedFiles = true)
		{
			Revision = revision;
			base.Title = revision.Message;
			SearchString = searchString;
			if (initializeChangedFiles)
			{
				ShowBorder = revision.ChangedFiles.Length != 0;
				ChangedFile[] changedFiles = revision.ChangedFiles;
				foreach (ChangedFile changedFile in changedFiles)
				{
					base.Children.Add(new SidebarSearchFileItem(revision, changedFile, searchString));
				}
			}
		}
	}
}
