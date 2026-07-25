using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using ForkPlus.UI;

namespace ForkPlus.UI.Controls
{
	// 阶段 4.5：WPF Adorner → Avalonia Control + OverlayLayer。
	// WPF AdornerLayer.Add/Remove → OverlayLayer.Children.Add/Remove（通过 AttachTo/DetachFrom 封装）。
	// WPF Adorner.AdornedElement.RenderSize → 缓存 _adornedSize。
	// WPF DrawingContext.DrawLine → Avalonia DrawingContext.DrawLine（签名兼容）。
	public class DropPlaceAdorner : Control
	{
		private static readonly Pen _pen = new Pen(Theme.AccentBrush, 2.0);

		private readonly DropPosition _dropPosition;

		private readonly ListBoxItem _listViewItem;

		private readonly Size _adornedSize;

		public DropPlaceAdorner(Control adornedElement, DropPosition position, ListBoxItem listViewItem)
		{
			IsHitTestVisible = false;
			_dropPosition = position;
			_listViewItem = listViewItem;
			_adornedSize = adornedElement.Bounds.Size;
			HorizontalAlignment = HorizontalAlignment.Stretch;
			VerticalAlignment = VerticalAlignment.Stretch;
		}

		public override void Render(DrawingContext context)
		{
			Rect rect = new Rect(_adornedSize);
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
