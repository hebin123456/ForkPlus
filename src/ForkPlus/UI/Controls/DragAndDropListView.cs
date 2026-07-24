// 阶段 4.5：WPF System.Windows.* → Avalonia.* 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Controls
// - DependencyObject → Control（Avalonia ItemsControl 容器基类型）
// - GetContainerForItemOverride() → CreateContainerForItemOverride()（Avalonia 命名）
// - PrepareContainerForItemOverride(DependencyObject, object) → PrepareContainerForItemOverride(Control, object)
using System;
using Avalonia;
using Avalonia.Controls;

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

		// 阶段 4.5：WPF GetContainerForItemOverride() → Avalonia CreateContainerForItemOverride()。
		// 返回类型 DependencyObject → Control。
		protected override Control CreateContainerForItemOverride()
		{
			return new DragAndDropListViewItem();
		}

		protected override bool IsItemItsOwnContainerOverride(object item)
		{
			return item is DragAndDropListViewItem;
		}

		// 阶段 4.5：WPF PrepareContainerForItemOverride(DependencyObject, object) → Avalonia PrepareContainerForItemOverride(Control, object)。
		protected override void PrepareContainerForItemOverride(Control element, object item)
		{
			base.PrepareContainerForItemOverride(element, item);
			(element as DragAndDropListViewItem).ParentListView = this;
		}
	}
}
