using Avalonia;
using ForkPlus.UI.WpfCompat;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class DropPlaceAdorner : Adorner
	{
		private static readonly Pen _pen = new Pen(global::ForkPlus.UI.Theme.AccentBrush, 2.0);

		private readonly DropPosition _dropPosition;

		private readonly global::Avalonia.Controls.ListBoxItem _listViewItem;

		public DropPlaceAdorner(global::Avalonia.Input.InputElement adornedElement, DropPosition position, global::Avalonia.Controls.ListBoxItem listViewItem)
			: base(adornedElement)
		{
			base.IsHitTestVisible = false;
			_dropPosition = position;
			_listViewItem = listViewItem;
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
			else if (_dropPosition == DropPosition.Over)
			{
				_listViewItem.Background = global::ForkPlus.UI.Theme.RevisionList.ItemSelectedInactiveBackgroundBrush;
			}
		}

		internal void ClearBackground()
		{
			if (_listViewItem.Background != global::ForkPlus.UI.Theme.RevisionList.ItemBackgroundBrush)
			{
				_listViewItem.Background = global::ForkPlus.UI.Theme.RevisionList.ItemBackgroundBrush;
			}
		}
	}
}
