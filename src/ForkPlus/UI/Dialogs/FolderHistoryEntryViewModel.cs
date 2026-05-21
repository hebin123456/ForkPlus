using ForkPlus.Git;
using ForkPlus.Git.Commands;

namespace ForkPlus.UI.Dialogs
{
	public class FolderHistoryEntryViewModel : HistoryEntryViewModel
	{
		public override bool IsFocusable => false;

		public FolderHistoryEntryViewModel(RevisionWithFiles revision, ChangedFile changedFile)
			: base(revision, changedFile)
		{
			base.ShowBorder = true;
		}
	}
}
