using System.IO;
using System.Windows.Media;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Dialogs
{
	public class SubItemFileHistoryEntryViewModel : HistoryEntryViewModel
	{
		public ImageSource FileTypeIcon { get; }

		public SubItemFileHistoryEntryViewModel(RevisionWithFiles revision, ChangedFile changedFile)
			: base(revision, changedFile)
		{
			FileTypeIcon = IconTools.GetImageSourceForExtension(System.IO.Path.GetExtension(changedFile.Path));
			base.ShowBorder = false;
		}
	}
}
