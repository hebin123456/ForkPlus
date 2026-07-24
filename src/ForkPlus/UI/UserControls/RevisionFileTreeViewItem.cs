using System;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.UserControls
{
	public class RevisionFileTreeViewItem : MultiselectionTreeViewItem
	{
		// 阶段 4.5：WPF BitmapImage + pack:// URI → Avalonia Bitmap + AssetLoader.Open（不可变，无需 Freeze）。
		private static readonly Bitmap FolderIcon = LoadAsset(new Uri("avares://ForkPlus/assets/folder.png"));

		private static Bitmap LoadAsset(Uri uri)
		{
			using (Stream stream = AssetLoader.Open(uri))
			{
				return new Bitmap(stream);
			}
		}

		private GitModule _gitModule;

		public FileTreeItem FileTreeItem { get; }

		public ImageSource FileTypeIcon { get; }

		public override bool ShowExpander
		{
			get
			{
				FileTreeItem fileTreeItem = FileTreeItem;
				if (fileTreeItem == null)
				{
					return false;
				}
				return fileTreeItem.ItemType == FileTreeItem.FileTreeItemType.Directory;
			}
		}

		public RevisionFileTreeViewItem(GitModule gitModule, FileTreeItem fileTreeItem)
		{
			FileTreeItem = fileTreeItem;
			_gitModule = gitModule;
			base.Title = FileTreeItem?.Filename;
			if (FileTreeItem != null)
			{
				FileTypeIcon = ((FileTreeItem.ItemType == FileTreeItem.FileTreeItemType.Directory) ? (FileTypeIcon = FolderIcon) : IconTools.GetImageSourceForExtension(Path.GetExtension(FileTreeItem.Filename)));
			}
		}

		protected override void OnExpanding()
		{
			base.OnExpanding();
			if (FileTreeItem == null)
			{
				return;
			}
			GitCommandResult<FileTreeItem[]> gitCommandResult = new GetRevisionFileTreeGitCommand().Execute(_gitModule, FileTreeItem.FilePath, FileTreeItem.TreeSha);
			if (gitCommandResult.Succeeded)
			{
				base.Children.Clear();
				FileTreeItem[] result = gitCommandResult.Result;
				foreach (FileTreeItem fileTreeItem in result)
				{
					base.Children.Add(new RevisionFileTreeViewItem(_gitModule, fileTreeItem));
				}
			}
		}
	}
}

