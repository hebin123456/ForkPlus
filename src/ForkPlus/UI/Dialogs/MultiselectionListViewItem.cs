using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace ForkPlus.UI.Dialogs
{
	public class MultiselectionListViewItem : ListViewItem
	{
		private bool _wasSelected;

		private Point _dragStartPoint;

		private DragAndDropListBoxAdorner _adorner;

		private DropPlaceAdorner _dropAdorner;

		public MultiselectionListView ParentListView { get; internal set; }

		public DropPosition DropPosition { get; internal set; }

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

		protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
		{
			e.Handled = true;
			base.OnMouseDoubleClick(e);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (!base.IsMouseCaptured)
			{
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
			ListViewItem[] array2 = array.CompactMap((RevisionEntry x) => ParentListView.ItemContainerGenerator.ContainerFromItem(x) as ListViewItem);
			ListBoxItem[] listBoxItems = array2;
			_adorner = new DragAndDropListBoxAdorner(this, listBoxItems, e.GetPosition(this));
			if (_adorner != null)
			{
				AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(ParentListView);
				if (adornerLayer != null)
				{
					adornerLayer.Add(_adorner);
					DragDrop.DoDragDrop(this, array, DragDropEffects.Move);
					adornerLayer.Remove(_adorner);
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
			double actualHeight = base.ActualHeight;
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
				AdornerLayer.GetAdornerLayer(ParentListView)?.Add(_dropAdorner);
			}
		}

		private void ClearDropAdorner()
		{
			if (_dropAdorner != null)
			{
				AdornerLayer.GetAdornerLayer(ParentListView)?.Remove(_dropAdorner);
			}
		}
	}
}
