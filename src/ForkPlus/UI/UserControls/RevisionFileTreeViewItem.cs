using System;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.UserControls
{
	public class RevisionFileTreeViewItem : MultiselectionTreeViewItem
	{
		private static readonly global::Avalonia.Media.Imaging.Bitmap FolderIcon = new global::Avalonia.Media.Imaging.Bitmap(global::Avalonia.Platform.AssetLoader.Open(new Uri("avares://ForkPlus/Assets/Folder.png")));

		private GitModule _gitModule;

		public FileTreeItem FileTreeItem { get; }

		public global::Avalonia.Media.IImage FileTypeIcon { get; }

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
			// v5.3.0（文件树过滤"整树补全"）：子项已加载时不再重复拉取——过滤激活时整树
			// 已一次性补全（含未展开目录），IsExpanded 展开不应再触发 git 调用；同时修正
			// 原实现"每次展开都重拉"的重复开销（树内容随 revision 切换重建，无过期问题）。
			if (base.Children.Count > 0)
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

