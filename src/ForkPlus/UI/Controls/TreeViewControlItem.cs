using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.Controls
{
	public class TreeViewControlItem : ListViewItem
	{
		private Point _startPoint;

		private bool _wasSelected;

		private DragAdorner _adorner;

		public MultiselectionTreeViewItem Node => base.DataContext as MultiselectionTreeViewItem;

		public MultiselectionTreeView ParentTreeView { get; internal set; }

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (e.Property == FrameworkElement.DataContextProperty)
			{
				UpdateDataContext(e.OldValue as MultiselectionTreeViewItem, e.NewValue as MultiselectionTreeViewItem);
			}
		}

		private void UpdateDataContext(MultiselectionTreeViewItem oldNode, MultiselectionTreeViewItem newNode)
		{
			if (newNode != null)
			{
				newNode.PropertyChanged += Node_PropertyChanged;
				if (base.Template != null)
				{
					UpdateTemplate();
				}
			}
			if (oldNode != null)
			{
				oldNode.PropertyChanged -= Node_PropertyChanged;
			}
		}

		private void Node_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "IsExpanded" && Node.IsExpanded)
			{
				ParentTreeView.HandleExpanding(Node);
			}
		}

		private void UpdateTemplate()
		{
		}

		internal double CalculateIndent()
		{
			int num = 19 * Node.Level;
			num -= 19;
			if (num < 0)
			{
				return 0.0;
			}
			return num;
		}

		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			_wasSelected = base.IsSelected;
			if (!base.IsSelected)
			{
				base.OnMouseLeftButtonDown(e);
			}
			if (ParentTreeView.AllowDragDrop && Mouse.LeftButton == MouseButtonState.Pressed)
			{
				_startPoint = e.GetPosition(null);
				CaptureMouse();
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (!base.IsMouseCaptured)
			{
				return;
			}
			Point position = e.GetPosition(null);
			if (!(Math.Abs(position.X - _startPoint.X) >= SystemParameters.MinimumHorizontalDragDistance) && !(Math.Abs(position.Y - _startPoint.Y) >= SystemParameters.MinimumVerticalDragDistance))
			{
				return;
			}
			_adorner = new DragAdorner(this, e.GetPosition(this));
			if (_adorner != null)
			{
				AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(ParentTreeView);
				if (adornerLayer != null)
				{
					adornerLayer.Add(_adorner);
					MultiselectionTreeViewItem[] nodes = ParentTreeView.GetTopLevelSelection().ToArray();
					Node?.StartDrag(this, nodes);
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

		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			ReleaseMouseCapture();
			if (_wasSelected)
			{
				base.OnMouseLeftButtonDown(e);
			}
		}

		protected override void OnDragEnter(DragEventArgs e)
		{
			ParentTreeView.HandleDragEnter(this, e);
		}

		protected override void OnDragOver(DragEventArgs e)
		{
			ParentTreeView.HandleDragOver(this, e);
		}

		protected override void OnDrop(DragEventArgs e)
		{
			ParentTreeView.HandleDrop(this, e);
		}

		protected override void OnDragLeave(DragEventArgs e)
		{
			ParentTreeView.HandleDragLeave(this, e);
		}
	}
}
