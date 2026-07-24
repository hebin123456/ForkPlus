using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.Controls
{
	// 阶段 4.5：WPF Adorner → Avalonia Control + OverlayLayer。
	// 渲染多个 ListBoxItem 的 VisualBrush 副本（拖拽多项时的幽灵图像堆叠）。
	// WPF VisualBrush(UIElement) → Avalonia.Media.VisualBrush(Visual)。
	// WPF ActualHeight → Avalonia Bounds.Height。
	// WPF Point.Offset → Avalonia Point + Vector。
	public class DragAndDropListViewAdorner : Control
	{
		private readonly double _visualBrushYOffset;

		private readonly VisualBrush[] _visualBrushes;

		private readonly Point _initialPosition;

		private readonly Size _adornedSize;

		private Point NewPosition { get; set; }

		public DragAndDropListViewAdorner(Control adornedElement, ListBoxItem[] listBoxItems, Point position)
		{
			_initialPosition = position;
			_adornedSize = adornedElement.Bounds.Size;
			_visualBrushes = listBoxItems.Map((ListBoxItem x) => new VisualBrush(x));
			_visualBrushYOffset = listBoxItems.FirstItem()?.Bounds.Height ?? 0.0;
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
			for (int i = 0; i < _visualBrushes.Length; i++)
			{
				VisualBrush brush = _visualBrushes[i];
				if (i > 0)
				{
					newPosition = newPosition + new Vector(0.0, _visualBrushYOffset);
				}
				using (context.PushOpacity(0.4))
				{
					context.DrawRectangle(brush, null, new Rect(newPosition, _adornedSize));
				}
			}
		}

		// 阶段 4.5：封装 OverlayLayer 挂载/卸载。
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
