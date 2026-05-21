using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ForkPlus.UI.Controls
{
	public class DropPlaceAdorner : Adorner
	{
		private static readonly Pen _pen = new Pen(Theme.AccentBrush, 2.0);

		private readonly DropPosition _dropPosition;

		private readonly ListViewItem _listViewItem;

		public DropPlaceAdorner(UIElement adornedElement, DropPosition position, ListViewItem listViewItem)
			: base(adornedElement)
		{
			base.IsHitTestVisible = false;
			_dropPosition = position;
			_listViewItem = listViewItem;
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
			else if (_dropPosition == DropPosition.Over)
			{
				_listViewItem.Background = Theme.RevisionList.ItemSelectedInactiveBackgroundBrush;
			}
		}

		internal void ClearBackground()
		{
			if (_listViewItem.Background != Theme.RevisionList.ItemBackgroundBrush)
			{
				_listViewItem.Background = Theme.RevisionList.ItemBackgroundBrush;
			}
		}
	}
}
