using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.UserControls.Preferences
{
	public class DragAndDropListBoxItem : ListBoxItem
	{
		private bool _wasSelected;

		private Point _dragStartPoint;

		private DragAndDropListBoxAdorner _adorner;

		private DropPlaceAdorner _dropAdorner;

		public DragAndDropListBox ParentListBox { get; internal set; }

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
			object[] array = ParentListBox.SelectedItems.CompactMap((object x) => x);
			if (array.Length < 1)
			{
				return;
			}
			ListBoxItem[] listBoxItems = array.CompactMap((object x) => ParentListBox.ItemContainerGenerator.ContainerFromItem(x) as ListBoxItem);
			_adorner = new DragAndDropListBoxAdorner(this, listBoxItems, e.GetPosition(this));
			if (_adorner != null)
			{
				AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(ParentListBox);
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
				AdornerLayer.GetAdornerLayer(ParentListBox)?.Add(_dropAdorner);
			}
		}

		private void ClearDropAdorner()
		{
			if (_dropAdorner != null)
			{
				AdornerLayer.GetAdornerLayer(ParentListBox)?.Remove(_dropAdorner);
			}
		}
	}
}
