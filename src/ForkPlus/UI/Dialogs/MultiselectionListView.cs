// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.* → using Avalonia.*
// - ListBox → Avalonia.Controls.ListBox
// - DependencyObject → Control（CreateContainerForItemOverride 返回类型）
// - 阶段 5：GetContainerForItemOverride → CreateContainerForItemOverride（3 参数）；IsItemItsOwnContainerOverride 不存在已移除
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

		// 阶段 5：Avalonia 11.3 CreateContainerForItemOverride 为 3 参数签名 (object? item, int index, object? recycleKey)。返回类型 Control。
		protected override Control CreateContainerForItemOverride(object item, int index, object recycleKey)
		{
			return new MultiselectionListViewItem();
		}

		// 阶段 5：Avalonia 11.3 无 IsItemItsOwnContainerOverride 虚方法，已移除（原 WPF 判断 item is MultiselectionListViewItem）。

		// 阶段 5：Avalonia 11.3 PrepareContainerForItemOverride 为 3 参数签名 (Control, object?, int)。
		protected override void PrepareContainerForItemOverride(Control element, object item, int index)
		{
			base.PrepareContainerForItemOverride(element, item, index);
			(element as MultiselectionListViewItem).ParentListView = this;
		}
	}
}
