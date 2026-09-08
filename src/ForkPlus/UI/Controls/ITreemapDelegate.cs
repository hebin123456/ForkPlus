using Avalonia;
using Avalonia.Media;
using ForkPlus.UI.Dialogs;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public interface ITreemapDelegate
	{
		string GetItemTitle(object array, int index);

		void DrawChildInRect(DrawingContext ctx, object items, int index, Rect rect, bool isHover, bool isSelected);

		[Null]
		TooltipView CreateTooltip(Treemap.IndexPath indexPath);
	}
}
