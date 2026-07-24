using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;

namespace ForkPlus.UI.Helpers
{
	// 阶段 4.5：WPF System.Windows.* → Avalonia.*。
	// WPF VisualTreeHelper.GetDpi(textBox).PixelsPerDip → 移除（Avalonia FormattedText 无此参数）。
	// WPF FormattedText 构造含 PixelsPerDip 参数 → Avalonia FormattedText 无此参数。
	// TODO(4.5): 验证 Avalonia FormattedText 在高 DPI 下的渲染一致性。
	public static class TextGuidelineHelper
	{
		public static double GuideLinePosition(TextBox textBox, int position)
		{
			return new FormattedText("w", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontConstants.MonospaceFontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch), textBox.FontSize, Brushes.Black).Width * (double)position + textBox.Padding.Left + 2.0;
		}
	}
}
