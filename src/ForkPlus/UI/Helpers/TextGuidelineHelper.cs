using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ForkPlus.UI.Helpers
{
	public static class TextGuidelineHelper
	{
		public static double GuideLinePosition(TextBox textBox, int position)
		{
			return new FormattedText("w", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontConstants.MonospaceFontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch), textBox.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(textBox).PixelsPerDip).Width * (double)position + textBox.Padding.Left + 2.0;
		}
	}
}
