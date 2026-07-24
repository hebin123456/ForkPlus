// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.Media → using Avalonia.Media
// - ImageSource → IImage
using System.IO;
using Avalonia.Media;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Dialogs
{
	public class SubItemFileHistoryEntryViewModel : HistoryEntryViewModel
	{
		public IImage FileTypeIcon { get; }

		public SubItemFileHistoryEntryViewModel(RevisionWithFiles revision, ChangedFile changedFile)
			: base(revision, changedFile)
		{
			FileTypeIcon = IconTools.GetImageSourceForExtension(System.IO.Path.GetExtension(changedFile.Path));
			base.ShowBorder = false;
		}
	}
}
