using Avalonia;
using ForkPlus.UI.WpfCompat;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Dialogs
{
	public class DropPlaceAdorner : Adorner
	{
		private static readonly Pen _pen = new Pen(global::ForkPlus.UI.Theme.AccentBrush, 2.0);

		private DropPosition _dropPosition;

		public DropPlaceAdorner(global::Avalonia.Input.InputElement adornedElement, DropPosition position)
			: base(adornedElement)
		{
			base.IsHitTestVisible = false;
			_dropPosition = position;
		}

		public override void Render(DrawingContext context)
		{
			Rect rect = new Rect(base.AdornedElement.Bounds.Size);
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
