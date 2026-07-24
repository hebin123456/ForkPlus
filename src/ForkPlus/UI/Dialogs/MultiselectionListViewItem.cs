// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.* → using Avalonia.*
// - OnMouseLeftButtonDown/Up/Move → OnPointerPressed/Released/Moved
// - Mouse.LeftButton == MouseButtonState.Pressed → e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
// - CaptureMouse/ReleaseMouseCapture/IsMouseCaptured → e.Pointer.Capture + _isPointerCaptured 字段
// - AdornerLayer.GetAdornerLayer(parent).Add/Remove → _adorner.AttachTo/DetachFrom(parent)
// - SystemParameters.Minimum*DragDistance → 常量 10.0
// - OnGiveFeedback 移除（Avalonia DoDragDrop 异步阻塞，无 GiveFeedback 事件）
// - ItemContainerGenerator.ContainerFromItem → ContainerFromItem
// - ActualHeight → Bounds.Height
// - Mouse.GetPosition() → PointerMoved 事件参数
// 参考已迁移的 DragAndDropListViewItem（Controls/）相同模式
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.Dialogs
{
	public class MultiselectionListViewItem : ListBoxItem
	{
		private bool _wasSelected;

		private Point _dragStartPoint;

		// 阶段 4.5：替代 WPF IsMouseCaptured，跟踪 Pointer 捕获状态。
		private bool _isPointerCaptured;

		private DragAndDropListBoxAdorner _adorner;

		private DropPlaceAdorner _dropAdorner;

		public MultiselectionListView ParentListView { get; internal set; }

		public DropPosition DropPosition { get; internal set; }

		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			_wasSelected = base.IsSelected;
			if (!base.IsSelected)
			{
				base.OnPointerPressed(e);
			}
			// 阶段 4.5：WPF Mouse.LeftButton == MouseButtonState.Pressed → Avalonia e.GetCurrentPoint(this).Properties.IsLeftButtonPressed。
			if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			{
				_dragStartPoint = e.GetPosition(null);
				// 阶段 4.5：WPF CaptureMouse() → Avalonia e.Pointer.Capture(this)。
				e.Pointer.Capture(this);
				_isPointerCaptured = true;
			}
		}

		protected override void OnPointerReleased(PointerReleasedEventArgs e)
		{
			// 阶段 4.5：WPF ReleaseMouseCapture() → Avalonia e.Pointer.Capture(null)。
			e.Pointer.Capture(null);
			_isPointerCaptured = false;
			if (_wasSelected)
			{
				// TODO(4.5): WPF 在 OnMouseLeftButtonUp 中调用 base.OnMouseLeftButtonDown(e) 以切换已选中项的选中状态。
				// Avalonia OnPointerPressed(PointerPressedEventArgs) 与 OnPointerReleased(PointerReleasedEventArgs) 参数类型不同，
				// 无法从 OnPointerReleased 直接调用 base.OnPointerPressed。阶段 6 需重新实现选中切换逻辑。
			}
		}

		protected override void OnDoubleTapped(TappedEventArgs e)
		{
			e.Handled = true;
			base.OnDoubleTapped(e);
		}

		protected override void OnPointerMoved(PointerEventArgs e)
		{
			// 阶段 4.5：WPF IsMouseCaptured → 自定义 _isPointerCaptured 字段。
			if (!_isPointerCaptured)
			{
				base.OnPointerMoved(e);
				return;
			}
			Point position = e.GetPosition(null);
			if (!ExceedDragDistance(_dragStartPoint - position))
			{
				return;
			}
			RevisionEntry[] array = ParentListView.SelectedItems.CompactMap((object x) => x as RevisionEntry);
			if (array.Length < 1)
			{
				return;
			}
			// 阶段 4.5：WPF ItemContainerGenerator.ContainerFromItem → Avalonia ContainerFromItem。
			ListBoxItem[] array2 = array.CompactMap((RevisionEntry x) => ParentListView.ContainerFromItem(x) as ListBoxItem);
			ListBoxItem[] listBoxItems = array2;
			_adorner = new DragAndDropListBoxAdorner(this, listBoxItems, e.GetPosition(this));
			if (_adorner != null)
			{
				// 阶段 4.5：WPF AdornerLayer.GetAdornerLayer(parent).Add/Remove → _adorner.AttachTo/DetachFrom(parent)。
				_adorner.AttachTo(ParentListView);
				// 阶段 4.5：WPF DragDrop.DoDragDrop(this, array, ...) — array 非 IDataObject。
				// Avalonia DataObject 无 (object) 构造函数；参考 ClosableTabItem/DragAndDropListViewItem 直接传 object。
				_ = DragDrop.DoDragDrop(this, array, DragDropEffects.Move);
				_adorner.DetachFrom(ParentListView);
			}
		}

		// TODO(4.5): WPF OnGiveFeedback 用于拖拽时实时更新 DragAdorner 位置。Avalonia DoDragDrop 异步阻塞，无法实时更新。阶段 6 考虑自定义拖拽逻辑替代。

		private static bool ExceedDragDistance(Vector diff)
		{
			// 阶段 4.5：WPF SystemParameters.MinimumHorizontalDragDistance/MinimumVerticalDragDistance → 常量 10.0。
			if (!(Math.Abs(diff.X) > 10.0))
			{
				return Math.Abs(diff.Y) > 10.0;
			}
			return true;
		}

		protected override void OnDragOver(DragEventArgs e)
		{
			ClearDropAdorner();
			DropPosition = GetDropPositoion(e);
			ShowDropAdorner(DropPosition);
		}

		protected override void OnDrop(DragEventArgs e)
		{
			ClearDropAdorner();
		}

		protected override void OnDragLeave(DragEventArgs e)
		{
			ClearDropAdorner();
		}

		private DropPosition GetDropPositoion(DragEventArgs e)
		{
			double y = e.GetPosition(this).Y;
			// 阶段 4.5：WPF ActualHeight → Avalonia Bounds.Height。
			double actualHeight = base.Bounds.Height;
			if (!(y < actualHeight / 2.0))
			{
				return DropPosition.Bottom;
			}
			return DropPosition.Top;
		}

		private void ShowDropAdorner(DropPosition dropPosition)
		{
			_dropAdorner = new DropPlaceAdorner(this, dropPosition);
			if (_dropAdorner != null)
			{
				_dropAdorner.AttachTo(ParentListView);
			}
		}

		private void ClearDropAdorner()
		{
			if (_dropAdorner != null)
			{
				_dropAdorner.DetachFrom(ParentListView);
			}
		}
	}
}
