using Avalonia;
using Avalonia.Controls;
using ForkPlus.UI.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Dialogs
{
	public class MultiselectionListView : global::Avalonia.Controls.ListBox
	{
		private readonly DragAutoScrollHelper _dragAutoScroll;

		public MultiselectionListView()
		{
			_dragAutoScroll = new DragAutoScrollHelper(this);
		}

		// Migration note：WPF GetContainerForItemOverride/IsItemItsOwnContainerOverride 在 Avalonia 12 无对应虚方法，
		// 死代码永不被调用 → 容器是默认 ListBoxItem，PrepareContainerForItemOverride 强转 null → NRE 吞掉容器生成。
		protected override global::Avalonia.Controls.Control CreateContainerForItemOverride(object item, int index, object recycleKey)
		{
			return new MultiselectionListViewItem();
		}

		protected override void PrepareContainerForItemOverride(global::Avalonia.Controls.Control element, object item, int index)
		{
			base.PrepareContainerForItemOverride(element, item, index);
			(element as MultiselectionListViewItem)?.ParentListView = this;
		}
	}
}
