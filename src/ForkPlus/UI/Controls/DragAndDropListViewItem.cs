using System;
using ForkPlus.UI.WpfCompat;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using ForkPlus.UI.Helpers;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	internal class DragAndDropListViewItem : global::Avalonia.Controls.ListBoxItem
	{
		private bool _wasSelected;

		private bool _handledPlainSelection;

		private Point _dragStartPoint;

		private DragAndDropListViewAdorner _adorner;

		private DropPlaceAdorner _dropAdorner;

		public DragAndDropListView ParentListView { get; internal set; }

		public DropPosition DropPosition { get; private set; }

		public bool AllowDrag { get; set; }

		public DragAndDropListViewItem()
		{
			AddHandler(DragDrop.DragEnterEvent, (_, e) => OnDragEnter(e));
			AddHandler(DragDrop.DragOverEvent, (_, e) => OnDragEnter(e));
			AddHandler(DragDrop.DragLeaveEvent, (_, e) => OnDragLeave(e));
			AddHandler(DragDrop.DropEvent, (_, e) => OnDrop(e));
		}

		protected override void OnPointerPressed(global::Avalonia.Input.PointerPressedEventArgs e)
		{
			_handledPlainSelection = false;
			_wasSelected = base.IsSelected;
			bool plainLeftClick = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
				&& !e.KeyModifiers.HasFlag(KeyModifiers.Control)
				&& !e.KeyModifiers.HasFlag(KeyModifiers.Shift);
			if (plainLeftClick && ParentListView != null && ParentListView.SelectionMode == SelectionMode.Multiple)
			{
				ParentListView.SelectedItems.Clear();
				base.OnPointerPressed(e);
				_handledPlainSelection = true;
				_wasSelected = true;
			}
			else if (!base.IsSelected)
			{
				base.OnPointerPressed(e);
			}
			if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			{
				_dragStartPoint = e.GetPosition(null);
				e.Pointer.Capture(this);
			}
		}

		protected override void OnPointerReleased(global::Avalonia.Input.PointerReleasedEventArgs e)
		{
			if (e.Pointer.Captured == this)
			{
				e.Pointer.Capture(null);
			}
			if (_wasSelected && !_handledPlainSelection)
			{
				// Migration note：WPF 原码在 OnMouseLeftButtonUp 里调 base.OnMouseLeftButtonDown(e)（点击已选项时补触发选择）。
				// Avalonia 12 需合成 PointerPressedEventArgs 才能复用 base.OnPointerPressed 的选择逻辑。
				global::Avalonia.Visual root = global::Avalonia.Controls.TopLevel.GetTopLevel(this);
				if (root != null)
				{
					global::Avalonia.Input.PointerPressedEventArgs pressed = new global::Avalonia.Input.PointerPressedEventArgs(this, e.Pointer, root, e.GetPosition(root), e.Timestamp, e.Properties, e.KeyModifiers, 1);
					base.OnPointerPressed(pressed);
				}
			}
		}

		protected override void OnPointerMoved(global::Avalonia.Input.PointerEventArgs e)
		{
			if (e.Pointer.Captured != this)
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
			global::Avalonia.Controls.ListBoxItem[] array2 = array.CompactMap((DecoratedRevision x) => ParentListView.ContainerFromItem(x) as global::Avalonia.Controls.ListBoxItem);
			ParentListView?.ItemDrag?.Invoke(this, EventArgs.Empty);
			if (AllowDrag)
			{
				ListBoxItem[] listBoxItems = array2;
				_adorner = new DragAndDropListViewAdorner(this, listBoxItems, e.GetPosition(this));
				AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(ParentListView);
				if (adornerLayer != null)
				{
					adornerLayer.Add(_adorner);
					global::ForkPlus.UI.WpfCompat.DragDropLauncher.DoDragDrop(this, array, DragDropEffects.Move);
					adornerLayer.Remove(_adorner);
					ParentListView.StopDragAutoScroll();
				}
			}
		}

		protected void OnGiveFeedback(GiveFeedbackEventArgs e)
		{
			if (base.IsVisible && _adorner != null)
			{
				Point position = this.PointFromScreen(MouseHelper.GetMousePosition());
				_adorner.UpdatePosition(position);
			}
		}

		private static bool ExceedDragDistance(Vector diff)
		{
			if (!(Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance))
			{
				return Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance;
			}
			return true;
		}

		protected void OnDragEnter(DragEventArgs e)
		{
			DecoratedRevision item = null;
			if ((e.Source as global::Avalonia.Controls.Presenters.ContentPresenter)?.Content is DecoratedRevision decoratedRevision) // Migration note：ContentPresenter 在 Avalonia.Controls.Presenters 命名空间。
			{
				item = decoratedRevision;
			}
			else if ((e.Source as Border)?.DataContext is DecoratedRevision decoratedRevision2)
			{
				item = decoratedRevision2;
			}
			if (ParentListView.ContainerFromItem(item) is global::Avalonia.Controls.ListBoxItem targetListViewItem)
			{
				ClearDropAdorner();
				DropPosition = GetDropPosition(e);
				ShowDropAdorner(DropPosition, targetListViewItem);
			}
		}

		protected void OnDrop(DragEventArgs e)
		{
			ClearDropAdorner();
		}

		protected void OnDragLeave(DragEventArgs e)
		{
			ClearDropAdorner();
		}

		private DropPosition GetDropPosition(DragEventArgs e)
		{
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

		private void ShowDropAdorner(DropPosition dropPosition, global::Avalonia.Controls.ListBoxItem targetListViewItem)
		{
			_dropAdorner = new DropPlaceAdorner(this, dropPosition, targetListViewItem);
			AdornerLayer.GetAdornerLayer(ParentListView)?.Add(_dropAdorner);
		}

		private void ClearDropAdorner()
		{
			if (_dropAdorner != null)
			{
				AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(ParentListView);
				if (adornerLayer != null)
				{
					_dropAdorner.ClearBackground();
					adornerLayer.Remove(_dropAdorner);
				}
			}
		}
	}
}
