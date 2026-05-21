using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ForkPlus.UI.Dialogs
{
	public class DropPlaceAdorner : Adorner
	{
		private static readonly Pen _pen = new Pen(Theme.AccentBrush, 2.0);

		private DropPosition _dropPosition;

		public DropPlaceAdorner(UIElement adornedElement, DropPosition position)
			: base(adornedElement)
		{
			base.IsHitTestVisible = false;
			_dropPosition = position;
		}

		protected override void OnRender(DrawingContext context)
		{
			Rect rect = new Rect(base.AdornedElement.RenderSize);
			if (_dropPosition == DropPosition.Top)
			{
				context.DrawLine(_pen, rect.TopLeft, rect.TopRight);
			}
			else if (_dropPosition == DropPosition.Bottom)
			{
				context.DrawLine(_pen, rect.BottomLeft, rect.BottomRight);
			}
		}
	}
}
