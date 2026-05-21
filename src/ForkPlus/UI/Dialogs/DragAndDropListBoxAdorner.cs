using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ForkPlus.UI.Dialogs
{
	public class DragAndDropListBoxAdorner : Adorner
	{
		private double _visualBrushYOffset;

		private Brush[] _visualBrushes;

		private Point _initialPosition;

		private Point NewPosition { get; set; }

		public DragAndDropListBoxAdorner(UIElement adornedElement, ListBoxItem[] listBoxItems, Point position)
			: base(adornedElement)
		{
			_initialPosition = position;
			Brush[] visualBrushes = listBoxItems.Map((ListBoxItem x) => new VisualBrush(x)
			{
				Opacity = 0.4
			});
			_visualBrushes = visualBrushes;
			_visualBrushYOffset = listBoxItems.FirstItem()?.ActualHeight ?? 0.0;
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
			for (int i = 0; i < _visualBrushes.Length; i++)
			{
				Brush brush = _visualBrushes[i];
				if (i > 0)
				{
					newPosition.Offset(0.0, _visualBrushYOffset);
				}
				context.DrawRectangle(brush, null, new Rect(newPosition, base.RenderSize));
			}
		}
	}
}
