// 阶段 4.5：WPF System.Windows.* → Avalonia.* 迁移。
// - using System.Windows → using Avalonia
// - using System.Windows.Controls → using Avalonia.Controls
// - using System.Windows.Documents → 移除（AdornerLayer 由 AttachTo/DetachFrom 替代）
// - using System.Windows.Input → using Avalonia.Input
// - 基类 ListViewItem → Avalonia.Controls.ListViewItem
// - OnPropertyChanged(DependencyPropertyChangedEventArgs) → OnPropertyChanged(AvaloniaPropertyChangedEventArgs)
// - FrameworkElement.DataContextProperty → Control.DataContextProperty
// - OnMouseLeftButtonDown/Up/Move → OnPointerPressed/Released/Moved
// - Mouse.LeftButton == MouseButtonState.Pressed → e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
// - CaptureMouse/ReleaseMouseCapture/IsMouseCaptured → e.Pointer.Capture + _isPointerCaptured 字段
// - AdornerLayer.GetAdornerLayer(parent).Add/Remove → _adorner.AttachTo/DetachFrom(parent)
// - SystemParameters.Minimum*DragDistance → 常量 10.0
// - OnGiveFeedback 移除（Avalonia DoDragDrop 异步阻塞，无 GiveFeedback 事件）
using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.Controls
{
	public class TreeViewControlItem : ListViewItem
	{
		private Point _startPoint;

		private bool _wasSelected;

		// 阶段 4.5：替代 WPF IsMouseCaptured，跟踪 Pointer 捕获状态。
		private bool _isPointerCaptured;

		private DragAdorner _adorner;

		public MultiselectionTreeViewItem Node => base.DataContext as MultiselectionTreeViewItem;

		public MultiselectionTreeView ParentTreeView { get; internal set; }

		// 阶段 4.5：WPF OnPropertyChanged(DependencyPropertyChangedEventArgs) → Avalonia OnPropertyChanged(AvaloniaPropertyChangedEventArgs)。
		// WPF FrameworkElement.DataContextProperty → Avalonia Control.DataContextProperty。
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (e.Property == Control.DataContextProperty)
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

		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			_wasSelected = base.IsSelected;
			if (!base.IsSelected)
			{
				base.OnPointerPressed(e);
			}
			// 阶段 4.5：WPF Mouse.LeftButton == MouseButtonState.Pressed → Avalonia e.GetCurrentPoint(this).Properties.IsLeftButtonPressed。
			if (ParentTreeView.AllowDragDrop && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			{
				_startPoint = e.GetPosition(null);
				// 阶段 4.5：WPF CaptureMouse() → Avalonia e.Pointer.Capture(this)。
				e.Pointer.Capture(this);
				_isPointerCaptured = true;
			}
		}

		protected override void OnPointerMoved(PointerEventArgs e)
		{
			// 阶段 4.5：WPF IsMouseCaptured → 自定义 _isPointerCaptured 字段。
			if (!_isPointerCaptured)
			{
				return;
			}
			Point position = e.GetPosition(null);
			// 阶段 4.5：WPF SystemParameters.MinimumDragDistance → 常量 10.0。
			if (!(Math.Abs(position.X - _startPoint.X) >= 10.0) && !(Math.Abs(position.Y - _startPoint.Y) >= 10.0))
			{
				return;
			}
			_adorner = new DragAdorner(this, e.GetPosition(this));
			if (_adorner != null)
			{
				// 阶段 4.5：WPF AdornerLayer.GetAdornerLayer(parent).Add/Remove → _adorner.AttachTo/DetachFrom(parent)。
				_adorner.AttachTo(ParentTreeView);
				MultiselectionTreeViewItem[] nodes = ParentTreeView.GetTopLevelSelection().ToArray();
				Node?.StartDrag(this, nodes);
				_adorner.DetachFrom(ParentTreeView);
			}
		}

		// TODO(4.5): WPF OnGiveFeedback 用于拖拽时实时更新 DragAdorner 位置。Avalonia DoDragDrop 异步阻塞，无法实时更新。阶段 6 考虑自定义拖拽逻辑替代。

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

		// TODO(4.5): ParentTreeView (MultiselectionTreeView) 尚未迁移到 Avalonia，其 HandleDrag* 方法仍接受 System.Windows.DragEventArgs。
		// 此处传入 Avalonia.Input.DragEventArgs，存在跨阶段类型不匹配；待 MultiselectionTreeView 迁移后自动消解。
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
