using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ForkPlus.UI.Controls
{
	public class DragAdorner : Adorner
	{
		private Brush _visualBrush;

		private Point _initialPosition;

		private Point NewPosition { get; set; }

		public DragAdorner(UIElement adornedElement, Point position)
			: base(adornedElement)
		{
			_initialPosition = position;
			_visualBrush = new VisualBrush(base.AdornedElement)
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

		protected override void OnRender(DrawingContext context)
		{
			Point newPosition = NewPosition;
			newPosition.Offset(0.0 - _initialPosition.X, 0.0 - _initialPosition.Y);
			context.DrawRectangle(_visualBrush, null, new Rect(newPosition, base.RenderSize));
		}
	}
}
