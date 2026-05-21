using System.Windows;
using System.Windows.Controls;

namespace ForkPlus.UI.UserControls.Preferences
{
	public class DragAndDropListBox : ListBox
	{
		protected override DependencyObject GetContainerForItemOverride()
		{
			return new DragAndDropListBoxItem();
		}

		protected override bool IsItemItsOwnContainerOverride(object item)
		{
			return item is DragAndDropListBoxItem;
		}

		protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
		{
			base.PrepareContainerForItemOverride(element, item);
			(element as DragAndDropListBoxItem).ParentListBox = this;
		}
	}
}
