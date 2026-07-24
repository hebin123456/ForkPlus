using Avalonia;
using Avalonia.Media;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.Controls
{
	// 阶段 4.5：WPF System.Windows.Rect/Media.DrawingContext → Avalonia.Rect/Media.DrawingContext。
	public interface ITreemapDelegate
	{
		string GetItemTitle(object array, int index);

		void DrawChildInRect(DrawingContext ctx, object items, int index, Rect rect, bool isHover, bool isSelected);

		[Null]
		TooltipView CreateTooltip(Treemap.IndexPath indexPath);
	}
}
