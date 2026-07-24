// 阶段 4.5：WPF Adorner → Avalonia Control。
// WPF Adorner 基类提供 GetVisualChild/AddVisualChild/RemoveVisualChild/VisualChildrenCount
// 和 AddLogicalChild/RemoveLogicalChild。Avalonia Visual/StyledElement 提供等价 API：
// - AddVisualChild/RemoveVisualChild → Avalonia.Visual.AddVisualChild/RemoveVisualChild
// - AddLogicalChild/RemoveLogicalChild → LogicalChildren.Add/Remove
// - GetVisualChild/VisualChildrenCount → 同名 override（Avalonia.Visual）
// WPF FrameworkElement → Avalonia.Control。
// WPF UIElement → Avalonia.Control。
// VisualTreeAttachmentHelper.PrepareForNewParent 已迁移（接受 AvaloniaObject）。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Media;
using ForkPlus.UI;

namespace ForkPlus.UI.Dialogs
{
	public class RewordAdorner : Control
	{
		private Control _child;

		public Control Child
		{
			get
			{
				return _child;
			}
			set
			{
				if (_child != value)
				{
					if (_child != null)
					{
						RemoveVisualChild(_child);
						LogicalChildren.Remove(_child);
					}
					if (value != null && !VisualTreeAttachmentHelper.PrepareForNewParent(value, GetType().Name + ".Child"))
					{
						value = null;
					}
					_child = value;
					if (_child != null)
					{
						LogicalChildren.Add(_child);
						AddVisualChild(_child);
					}
					InvalidateMeasure();
				}
			}
		}

		protected override int VisualChildrenCount => (Child != null) ? 1 : 0;

		// 阶段 4.5：原 WPF Adorner 构造接收被装饰元素；Avalonia 无 Adorner 基类，
		// 保留参数以兼容调用方，但不再持有引用（布局由调用方挂载位置决定）。
		public RewordAdorner(Control adornernedElement)
		{
		}

		protected override Visual GetVisualChild(int index)
		{
			return Child;
		}

		protected override Size MeasureOverride(Size constraint)
		{
			if (Child == null)
			{
				return default(Size);
			}
			Child.Measure(constraint);
			Size result = Child.DesiredSize;
			if (result.Width < 40.0)
			{
				result = new Size(40.0, result.Height);
			}
			return result;
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			if (Child == null)
			{
				return default(Size);
			}
			Child.Arrange(new Rect(finalSize));
			return finalSize;
		}

		// 阶段 4.5：封装 OverlayLayer 挂载/卸载，替代 WPF AdornerLayer.Add/Remove。
		public void AttachTo(Control parent)
		{
			OverlayLayer overlay = OverlayLayer.GetOverlayLayer(parent);
			overlay?.Children.Add(this);
		}

		public void DetachFrom(Control parent)
		{
			OverlayLayer overlay = OverlayLayer.GetOverlayLayer(parent);
			overlay?.Children.Remove(this);
		}
	}
}
