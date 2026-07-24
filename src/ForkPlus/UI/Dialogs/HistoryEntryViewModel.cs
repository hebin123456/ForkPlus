// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.Media → using Avalonia.Media
// - ImageSource → IImage
using System;
using Avalonia.Media;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.Dialogs
{
	public class HistoryEntryViewModel : MultiselectionTreeViewItem
	{
		public ChangedFile ChangedFile { get; }

		public RevisionWithFiles Revision { get; }

		public UserIdentity Author => Revision.Author;

		public string AuthorName => Author.Name;

		public DateTime AuthorDate => Revision.AuthorDate;

		public Sha Sha => Revision.Sha;

		public string AbbreviatedSha => Revision.Sha.ToAbbreviatedString();

		public string RevisionSubject => Revision.Message;

		public string Path => ChangedFile.Path;

		public string OldPath => ChangedFile.OldPath;

		public StatusType Status => ChangedFile.Status;

		public IImage StatusImage { get; }

		public string OpenInSeparateWindowButtonToolTip => "Open '" + Revision.Sha.ToAbbreviatedString() + "' in separate window";

		public bool ShowBorder { get; set; }

		public HistoryEntryViewModel(RevisionWithFiles revision, ChangedFile changedFile)
		{
			Revision = revision;
			ChangedFile = changedFile;
			StatusImage = changedFile.Status.GetImageSource();
			ShowBorder = true;
		}
	}
}
