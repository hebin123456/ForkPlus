using System;
using System.Windows;

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

		protected override DependencyObject GetContainerForItemOverride()
		{
			return new DragAndDropListViewItem();
		}

		protected override bool IsItemItsOwnContainerOverride(object item)
		{
			return item is DragAndDropListViewItem;
		}

		protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
		{
			base.PrepareContainerForItemOverride(element, item);
			(element as DragAndDropListViewItem).ParentListView = this;
		}
	}
}
