using System.Windows;
using System.Windows.Controls;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.Dialogs
{
	public class MultiselectionListView : ListView
	{
		private readonly DragAutoScrollHelper _dragAutoScroll;

		public MultiselectionListView()
		{
			_dragAutoScroll = new DragAutoScrollHelper(this);
		}

		protected override DependencyObject GetContainerForItemOverride()
		{
			return new MultiselectionListViewItem();
		}

		protected override bool IsItemItsOwnContainerOverride(object item)
		{
			return item is MultiselectionListViewItem;
		}

		protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
		{
			base.PrepareContainerForItemOverride(element, item);
			(element as MultiselectionListViewItem).ParentListView = this;
		}
	}
}
