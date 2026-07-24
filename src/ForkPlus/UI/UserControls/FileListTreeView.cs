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

		public FileListTreeView()
		{
			// 阶段 5：基类 MultiselectionTreeView 已将 OnDrag*/OnDrop 改为 AddHandler 私有处理器，
			// 此处不能 override，需在子类自行 AddHandler 订阅路由事件。
			AddHandler(DragDrop.DragOverEvent, OnDragOver);
			AddHandler(DragDrop.DropEvent, OnDrop);
		}

		private void OnDragOver(object sender, DragEventArgs e)
		{
			// 阶段 4.5：WPF e.Effects → Avalonia e.DragEffects。
			e.DragEffects = DragDropEffects.None;
			// 阶段 4.5：WPF e.Data.GetData → Avalonia e.Data.Get。
			if (e.Data.Get(DragItemsFormat) is MultiselectionTreeViewItem[])
			{
				// 阶段 5：base.OnDragOver(e) 已不可用（基类改为私有处理器）。
				// 失去基类的 drop target 视觉反馈，阶段 6 需自行调用 HandleDragOver 或迁移 Adorner 逻辑。
				e.Handled = true;
				e.DragEffects = DragDropEffects.Move;
			}
		}

		private void OnDrop(object sender, DragEventArgs e)
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
