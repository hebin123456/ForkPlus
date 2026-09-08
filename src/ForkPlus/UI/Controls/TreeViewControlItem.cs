using System;
using ForkPlus.UI.WpfCompat;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using ForkPlus.UI.Helpers;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class TreeViewControlItem : global::Avalonia.Controls.ListBoxItem
	{
		private Point _startPoint;

		private bool _wasSelected;

		private DragAdorner _adorner;

		public MultiselectionTreeViewItem Node => base.DataContext as MultiselectionTreeViewItem;

		public MultiselectionTreeView ParentTreeView { get; internal set; }

		protected override void OnPropertyChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (e.Property == global::Avalonia.Controls.Control.DataContextProperty)
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
			int num = 10 * Node.Level;
			num -= 10;
			if (num < 0)
			{
				return 0.0;
			}
			return num;
		}

		protected override void OnPointerPressed(global::Avalonia.Input.PointerPressedEventArgs e)
		{
			if (ParentTreeView != null)
			{
				ParentTreeView.LastClickedItem = Node;
			}
			_wasSelected = base.IsSelected;
			if (!base.IsSelected)
			{
				base.OnPointerPressed(e);
			}
			if (ParentTreeView.AllowDragDrop && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			{
				_startPoint = e.GetPosition(null);
				e.Pointer.Capture(this);
			}
		}

		protected override void OnPointerMoved(global::Avalonia.Input.PointerEventArgs e)
		{
			if (e.Pointer.Captured != this)
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

		protected void OnGiveFeedback(GiveFeedbackEventArgs e)
		{
			if (base.IsVisible && _adorner != null)
			{
				Point position = this.PointFromScreen(MouseHelper.GetMousePosition());
				_adorner.UpdatePosition(position);
			}
		}

		protected override void OnPointerReleased(global::Avalonia.Input.PointerReleasedEventArgs e)
		{
			if (e.Pointer.Captured == this)
			{
				e.Pointer.Capture(null);
			}
			if (_wasSelected)
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

		protected void OnDragEnter(DragEventArgs e)
		{
			ParentTreeView.HandleDragEnter(this, e);
		}

		protected void OnDragOver(DragEventArgs e)
		{
			ParentTreeView.HandleDragOver(this, e);
		}

		protected void OnDrop(DragEventArgs e)
		{
			ParentTreeView.HandleDrop(this, e);
		}

		protected void OnDragLeave(DragEventArgs e)
		{
			ParentTreeView.HandleDragLeave(this, e);
		}
	}
}
