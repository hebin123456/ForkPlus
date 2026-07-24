using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace ForkPlus.UI.Controls
{
	// 阶段 4.5：WPF Adorner → Avalonia Control + OverlayLayer。
	// WPF AdornerLayer.GetAdornerLayer(parent).Add/Remove → OverlayLayer.GetOverlayLayer(parent).Children.Add/Remove。
	// WPF OnRender(DrawingContext) → Avalonia Render(DrawingContext)。
	// WPF VisualBrush(UIElement) → Avalonia.Media.VisualBrush(Visual)（Control 继承自 Visual）。
	// WPF Brush.Opacity → DrawingContext.PushOpacity（避免依赖画刷可变性）。
	// WPF Adorner.RenderSize（被装饰元素尺寸）→ 缓存 _adornedSize（adornedElement.Bounds.Size）。
	// WPF Point.Offset → Avalonia Point + Vector。
	// 注意：Avalonia DragDrop.DoDragDrop 是异步阻塞，拖拽期间无法通过 GiveFeedback 实时更新位置。
	// 调用方在 DoDragDrop 前挂载 adorner，通过 PointerMoved 跟踪位置（DoDragDrop 启动前的移动）。
	public class DragAdorner : Control
	{
		private VisualBrush _visualBrush;

		private Point _initialPosition;

		private Size _adornedSize;

		private Point NewPosition { get; set; }

		public DragAdorner(Control adornedElement, Point position)
		{
			_initialPosition = position;
			_adornedSize = adornedElement.Bounds.Size;
			_visualBrush = new VisualBrush(adornedElement);
			IsHitTestVisible = false;
			HorizontalAlignment = HorizontalAlignment.Stretch;
			VerticalAlignment = VerticalAlignment.Stretch;
		}

		public void UpdatePosition(Point position)
		{
			NewPosition = position;
			InvalidateVisual();
		}

		public override void Render(DrawingContext context)
		{
			Point newPosition = NewPosition + new Vector(0.0 - _initialPosition.X, 0.0 - _initialPosition.Y);
			using (context.PushOpacity(0.6))
			{
				context.DrawRectangle(_visualBrush, null, new Rect(newPosition, _adornedSize));
			}
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
