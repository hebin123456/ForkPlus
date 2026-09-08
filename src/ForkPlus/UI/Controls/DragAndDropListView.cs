using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	internal class DragAndDropListView : NoUIAutomationListView
	{
		public EventHandler<EventArgs> ItemDrag;

		private readonly DragAutoScrollHelper _dragAutoScroll;

		public DragAndDropListView()
		{
			_dragAutoScroll = new DragAutoScrollHelper(this);
		}

		internal void StopDragAutoScroll()
		{
			_dragAutoScroll.StopAutoScroll();
		}

		// Migration note：WPF ItemsControl.GetContainerForItemOverride()（返回 ItemContainer）在
		// Avalonia 12 无此虚方法；对应机制是 CreateContainerForItemOverride(item, index, recycleKey)。
		// 原非 override 的 GetContainerForItemOverride 永远不会被框架调用 → 实际容器是 ListBox
		// 默认 ListBoxItem（非 DragAndDropListViewItem），PrepareContainerForItemOverride 里
		// (element as DragAndDropListViewItem) 为 null → NRE 被集合事件链吞掉 → 容器永不生成
		// （提交列表空白实证，迁移期已修复）。
		// 注：WPF IsItemItsOwnContainerOverride 在 Avalonia 12 由 NeedsContainerOverride 取代。
		protected override global::Avalonia.Controls.Control CreateContainerForItemOverride(object item, int index, object recycleKey)
		{
			return new DragAndDropListViewItem();
		}

		protected override void PrepareContainerForItemOverride(global::Avalonia.Controls.Control element, object item, int index)
		{
			base.PrepareContainerForItemOverride(element, item, index);
			if (element is DragAndDropListViewItem listViewItem)
			{
				listViewItem.ParentListView = this;
				Avalonia.Input.DragDrop.SetAllowDrop(listViewItem, true);
			}
		}
	}
}
