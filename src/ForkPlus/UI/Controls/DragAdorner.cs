using Avalonia;
using ForkPlus.UI.WpfCompat;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class DragAdorner : Adorner
	{
		private Brush _visualBrush;

		private Point _initialPosition;

		private Point NewPosition { get; set; }

		public DragAdorner(global::Avalonia.Input.InputElement adornedElement, Point position)
			: base(adornedElement)
		{
			_initialPosition = position;
			// Migration note：WPF VisualBrush(visual) 在 Avalonia 12.1 为 Avalonia.Media.VisualBrush(Visual)，
			// 反编译产物里误写成不存在的 Avalonia.Media.ImmutableBrush。
			_visualBrush = new global::Avalonia.Media.VisualBrush(base.AdornedElement)
			{
				Opacity = 0.6
			};
			base.IsHitTestVisible = false;
		}

		public void UpdatePosition(Point position)
		{
			NewPosition = position;
			InvalidateVisual();
		}

		public override void Render(DrawingContext context)
		{
			Point newPosition = NewPosition;
			// Avalonia Point 无 Offset(原地修改)，用新 Point 等价改写。
			newPosition = new Point(newPosition.X - _initialPosition.X, newPosition.Y - _initialPosition.Y);
			context.DrawRectangle(_visualBrush, null, new Rect(newPosition, base.Bounds.Size));
		}
	}
}
