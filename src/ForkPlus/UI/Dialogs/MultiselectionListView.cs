// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.* → using Avalonia.*
// - ListBox → Avalonia.Controls.ListBox
// - DependencyObject → AvaloniaObject（GetContainerForItemOverride 返回类型）
// - GetContainerForItemOverride/IsItemItsOwnContainerOverride/PrepareContainerForItemOverride（API 兼容）
using Avalonia;
using Avalonia.Controls;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.Dialogs
{
	public class MultiselectionListView : ListBox
	{
		private readonly DragAutoScrollHelper _dragAutoScroll;

		public MultiselectionListView()
		{
			_dragAutoScroll = new DragAutoScrollHelper(this);
		}

		protected override AvaloniaObject GetContainerForItemOverride()
		{
			return new MultiselectionListViewItem();
		}

		protected override bool IsItemItsOwnContainerOverride(object item)
		{
			return item is MultiselectionListViewItem;
		}

		protected override void PrepareContainerForItemOverride(AvaloniaObject element, object item)
		{
			base.PrepareContainerForItemOverride(element, item);
			(element as MultiselectionListViewItem).ParentListView = this;
		}
	}
}
