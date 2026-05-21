using System;
using System.Windows;
using ForkPlus.Git;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.UserControls
{
	public class FileListTreeView : MultiselectionTreeView
	{
		public class DropEventArgs : EventArgs
		{
			public ChangedFile[] Files { get; private set; }

			public DropEventArgs(ChangedFile[] files)
			{
				Files = files;
			}
		}

		public static readonly string DragItemsFormat = "FileListItems";

		public EventHandler<DropEventArgs> ItemsDrop;

		protected override void OnDragOver(DragEventArgs e)
		{
			e.Effects = DragDropEffects.None;
			if (e.Data.GetData(DragItemsFormat) is MultiselectionTreeViewItem[])
			{
				base.OnDragOver(e);
				e.Handled = true;
				e.Effects = DragDropEffects.Move;
			}
		}

		protected override void OnDrop(DragEventArgs e)
		{
			e.Effects = DragDropEffects.None;
			if (e.Data.GetData(DragItemsFormat) is MultiselectionTreeViewItem[] source)
			{
				e.Handled = true;
				e.Effects = DragDropEffects.Move;
				ChangedFile[] files = source.CompactMap((MultiselectionTreeViewItem x) => (x as FileListItem)?.ChangedFile);
				ItemsDrop?.Invoke(this, new DropEventArgs(files));
			}
		}
	}
}
