using System;
using Avalonia;
using ForkPlus.Git;
using ForkPlus.UI.Controls;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Input;

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

		protected void OnDragOver(DragEventArgs e)
		{
			e.DragEffects= DragDropEffects.None;
			if (e.WpfData().GetData(DragItemsFormat) is MultiselectionTreeViewItem[])
			{
				base.OnDragOver(e);
				e.Handled = true;
				e.DragEffects= DragDropEffects.Move;
			}
		}

		protected void OnDrop(DragEventArgs e)
		{
			e.DragEffects= DragDropEffects.None;
			if (e.WpfData().GetData(DragItemsFormat) is MultiselectionTreeViewItem[] source)
			{
				e.Handled = true;
				e.DragEffects= DragDropEffects.Move;
				ChangedFile[] files = source.CompactMap((MultiselectionTreeViewItem x) => (x as FileListItem)?.ChangedFile);
				ItemsDrop?.Invoke(this, new DropEventArgs(files));
			}
		}
	}
}
