// 阶段 4.5：WPF System.Windows.* → Avalonia.* 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Controls
// - DependencyObject → Control（Avalonia ItemsControl 容器基类型）
// - GetContainerForItemOverride() → CreateContainerForItemOverride()（Avalonia 命名）
// - PrepareContainerForItemOverride(DependencyObject, object) → PrepareContainerForItemOverride(Control, object)
// 阶段 5：Avalonia 11.3 ItemsControl 容器生成 API 再次变更（相对 11.2）：
// - CreateContainerForItemOverride() → CreateContainerForItemOverride(object?, int, object?)
// - IsItemItsOwnContainerOverride(object) → NeedsContainerOverride(object?, int, out object?)
// - PrepareContainerForItemOverride(Control, object) → PrepareContainerForItemOverride(Control, object?, int)
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
		// 阶段 5：Avalonia 11.3 签名增加 item/index/recycleKey 参数。
		protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
		{
			return new DragAndDropListViewItem();
		}

		// 阶段 5：Avalonia 11.3 移除 IsItemItsOwnContainerOverride，由 NeedsContainerOverride 替代。
		// NeedsContainer<T>：item is T → false（无需容器）；否则 → true（需要容器）。
		protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
		{
			return NeedsContainer<DragAndDropListViewItem>(item, out recycleKey);
		}

		// 阶段 4.5：WPF PrepareContainerForItemOverride(DependencyObject, object) → Avalonia PrepareContainerForItemOverride(Control, object)。
		// 阶段 5：Avalonia 11.3 签名增加 index 参数（3 参数版本）。
		protected override void PrepareContainerForItemOverride(Control element, object? item, int index)
		{
			base.PrepareContainerForItemOverride(element, item, index);
			(element as DragAndDropListViewItem).ParentListView = this;
		}
	}
}
