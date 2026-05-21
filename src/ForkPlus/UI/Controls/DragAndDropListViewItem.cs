using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace ForkPlus.UI.Controls
{
	internal class DragAndDropListViewItem : ListViewItem
	{
		private bool _wasSelected;

		private Point _dragStartPoint;

		private DragAndDropListViewAdorner _adorner;

		private DropPlaceAdorner _dropAdorner;

		public DragAndDropListView ParentListView { get; internal set; }

		public DropPosition DropPosition { get; private set; }

		public bool AllowDrag { get; set; }

		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			_wasSelected = base.IsSelected;
			if (!base.IsSelected)
			{
				base.OnMouseLeftButtonDown(e);
			}
			if (Mouse.LeftButton == MouseButtonState.Pressed)
			{
				_dragStartPoint = e.GetPosition(null);
				CaptureMouse();
			}
		}

		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			ReleaseMouseCapture();
			if (_wasSelected)
			{
				base.OnMouseLeftButtonDown(e);
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (!base.IsMouseCaptured)
			{
				base.OnMouseMove(e);
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
			ListViewItem[] array2 = array.CompactMap((DecoratedRevision x) => ParentListView.ItemContainerGenerator.ContainerFromItem(x) as ListViewItem);
			ParentListView?.ItemDrag?.Invoke(this, EventArgs.Empty);
			if (AllowDrag)
			{
				ListBoxItem[] listBoxItems = array2;
				_adorner = new DragAndDropListViewAdorner(this, listBoxItems, e.GetPosition(this));
				AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(ParentListView);
				if (adornerLayer != null)
				{
					adornerLayer.Add(_adorner);
					DragDrop.DoDragDrop(this, array, DragDropEffects.Move);
					adornerLayer.Remove(_adorner);
					ParentListView.StopDragAutoScroll();
				}
			}
		}

		protected override void OnGiveFeedback(GiveFeedbackEventArgs e)
		{
			if (base.IsVisible && _adorner != null)
			{
				Point position = PointFromScreen(MouseHelper.GetMousePosition());
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
			if (ParentListView.ItemContainerGenerator.ContainerFromItem(item) is ListViewItem targetListViewItem)
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
			double actualHeight = base.ActualHeight;
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
