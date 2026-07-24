// 阶段 4.5：WPF System.Windows.* → Avalonia.* 迁移。
// - using System.Windows → using Avalonia
// - using System.Windows.Controls → using Avalonia.Controls
// - using System.Windows.Documents → 移除（AdornerLayer 由 AttachTo/DetachFrom 替代）
// - using System.Windows.Input → using Avalonia.Input
// - 基类 ListViewItem → Avalonia.Controls.ListViewItem
// - OnMouseLeftButtonDown/Up/Move → OnPointerPressed/Released/Moved
// - Mouse.LeftButton == MouseButtonState.Pressed → e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
// - CaptureMouse/ReleaseMouseCapture/IsMouseCaptured → e.Pointer.Capture + _isPointerCaptured 字段
// - AdornerLayer.GetAdornerLayer(parent).Add/Remove → _adorner.AttachTo/DetachFrom(parent)
// - SystemParameters.Minimum*DragDistance → 常量 10.0
// - OnGiveFeedback 移除（Avalonia DoDragDrop 异步阻塞，无 GiveFeedback 事件）
// - ItemContainerGenerator.ContainerFromItem → ContainerFromItem
// - ActualHeight → Bounds.Height
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.Controls
{
	internal class DragAndDropListViewItem : ListViewItem
	{
		private bool _wasSelected;

		private Point _dragStartPoint;

		// 阶段 4.5：替代 WPF IsMouseCaptured，跟踪 Pointer 捕获状态。
		private bool _isPointerCaptured;

		private DragAndDropListViewAdorner _adorner;

		private DropPlaceAdorner _dropAdorner;

		public DragAndDropListView ParentListView { get; internal set; }

		public DropPosition DropPosition { get; private set; }

		public bool AllowDrag { get; set; }

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
				// base.OnPointerPressed(e);
			}
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
			DecoratedRevision[] array = ParentListView.SelectedItems.CompactMap((object x) => x as DecoratedRevision);
			if (array.Length != 1)
			{
				return;
			}
			// TODO(4.5): 验证 Avalonia ContainerFromItem。
			ListViewItem[] array2 = array.CompactMap((DecoratedRevision x) => ParentListView.ContainerFromItem(x) as ListViewItem);
			ParentListView?.ItemDrag?.Invoke(this, EventArgs.Empty);
			if (AllowDrag)
			{
				ListBoxItem[] listBoxItems = array2;
				_adorner = new DragAndDropListViewAdorner(this, listBoxItems, e.GetPosition(this));
				// 阶段 4.5：WPF AdornerLayer.GetAdornerLayer(parent).Add/Remove → _adorner.AttachTo/DetachFrom(parent)。
				_adorner.AttachTo(ParentListView);
				// TODO(4.5): WPF DragDrop.DoDragDrop(this, array, ...) — array 非 IDataObject。
				// Avalonia DataObject 无 (object) 构造函数；参考 ClosableTabItem 直接传 object（框架内部包装）。
				_ = DragDrop.DoDragDrop(this, array, DragDropEffects.Move);
				_adorner.DetachFrom(ParentListView);
				ParentListView.StopDragAutoScroll();
			}
		}

		// TODO(4.5): WPF OnGiveFeedback 用于拖拽时实时更新 DragAdorner 位置。Avalonia DoDragDrop 异步阻塞，无法实时更新。阶段 6 考虑自定义拖拽逻辑替代。

		private static bool ExceedDragDistance(Vector diff)
		{
			// 阶段 4.5：WPF SystemParameters.MinimumDragDistance → 常量 10.0。
			if (!(Math.Abs(diff.X) > 10.0))
			{
				return Math.Abs(diff.Y) > 10.0;
			}
			return true;
		}

		protected override void OnDragEnter(DragEventArgs e)
		{
			DecoratedRevision item = null;
			if ((e.Source as ContentPresenter)?.Content is DecoratedRevision decoratedRevision)
			{
				item = decoratedRevision;
			}
			else if ((e.Source as Border)?.DataContext is DecoratedRevision decoratedRevision2)
			{
				item = decoratedRevision2;
			}
			// TODO(4.5): 验证 Avalonia ContainerFromItem。
			if (ParentListView.ContainerFromItem(item) is ListViewItem targetListViewItem)
			{
				ClearDropAdorner();
				DropPosition = GetDropPosition(e);
				ShowDropAdorner(DropPosition, targetListViewItem);
			}
		}

		protected override void OnDrop(DragEventArgs e)
		{
			ClearDropAdorner();
		}

		protected override void OnDragLeave(DragEventArgs e)
		{
			ClearDropAdorner();
		}

		private DropPosition GetDropPosition(DragEventArgs e)
		{
			// 阶段 4.5：WPF ActualHeight → Avalonia Bounds.Height。
			double actualHeight = base.Bounds.Height;
			double y = e.GetPosition(this).Y;
			double num = 3.0;
			if (y < num)
			{
				return DropPosition.Top;
			}
			if (y > actualHeight - num)
			{
				return DropPosition.Bottom;
			}
			return DropPosition.Over;
		}

		private void ShowDropAdorner(DropPosition dropPosition, ListViewItem targetListViewItem)
		{
			_dropAdorner = new DropPlaceAdorner(this, dropPosition, targetListViewItem);
			// 阶段 4.5：WPF AdornerLayer.GetAdornerLayer(parent)?.Add → _dropAdorner.AttachTo(parent)。
			_dropAdorner.AttachTo(ParentListView);
		}

		private void ClearDropAdorner()
		{
			if (_dropAdorner != null)
			{
				// 阶段 4.5：WPF AdornerLayer.GetAdornerLayer(parent)?.Remove → _dropAdorner.DetachFrom(parent)。
				_dropAdorner.ClearBackground();
				_dropAdorner.DetachFrom(ParentListView);
			}
		}
	}
}
