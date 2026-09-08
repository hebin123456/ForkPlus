using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Helpers
{
	public static class TextGuidelineHelper
	{
		public static double GuideLinePosition(TextBox textBox, int position)
                {
                        // Migration note：WPF FormattedText 第 7 参 PixelsPerDip 在 Avalonia 不存在，直接省略。
                        return new FormattedText("w", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontConstants.MonospaceFontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch), textBox.FontSize, Brushes.Black).Width * (double)position + textBox.Padding.Left + 2.0;
                }
	}
}
