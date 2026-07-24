// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.Media → using Avalonia.Media
// - ImageSource → IImage
using System.IO;
using Avalonia.Media;
using ForkPlus.Git;
using ForkPlus.Git.Commands;

namespace ForkPlus.UI.UserControls
{
	public class SidebarSearchFileItem : SidebarSearchItem
	{
		public ChangedFile ChangedFile { get; }

		public IImage StatusImage { get; }

		public IImage FileTypeIcon { get; }

		public SidebarSearchFileItem(RevisionWithFiles revision, ChangedFile changedFile, string searchString)
			: base(revision, searchString, initializeChangedFiles: false)
		{
			ChangedFile = changedFile;
			base.Title = changedFile.Path;
			StatusImage = changedFile.Status.GetImageSource();
			FileTypeIcon = IconTools.GetImageSourceForExtension(Path.GetExtension(changedFile.Path));
		}
	}
}
