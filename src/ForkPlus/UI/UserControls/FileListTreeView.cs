// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Input
// - e.Effects → e.DragEffects（Avalonia DragEventArgs 属性名）
// - e.Data.GetData(format) → e.Data.Get(format)（Avalonia IDataObject 方法名）
// 基类 MultiselectionTreeView 已迁移，OnDragOver/OnDrop 签名兼容。
using System;
using Avalonia.Input;
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
			// 阶段 4.5：WPF e.Effects → Avalonia e.DragEffects。
			e.DragEffects = DragDropEffects.None;
			// 阶段 4.5：WPF e.Data.GetData → Avalonia e.Data.Get。
			if (e.Data.Get(DragItemsFormat) is MultiselectionTreeViewItem[])
			{
				base.OnDragOver(e);
				e.Handled = true;
				e.DragEffects = DragDropEffects.Move;
			}
		}

		protected override void OnDrop(DragEventArgs e)
		{
			e.DragEffects = DragDropEffects.None;
			if (e.Data.Get(DragItemsFormat) is MultiselectionTreeViewItem[] source)
			{
				e.Handled = true;
				e.DragEffects = DragDropEffects.Move;
				ChangedFile[] files = source.CompactMap((MultiselectionTreeViewItem x) => (x as FileListItem)?.ChangedFile);
				ItemsDrop?.Invoke(this, new DropEventArgs(files));
			}
		}
	}
}
