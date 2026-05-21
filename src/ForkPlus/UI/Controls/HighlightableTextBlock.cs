using System.Windows;
using System.Windows.Controls;

namespace ForkPlus.UI.Controls
{
	internal class HighlightableTextBlock : TextBlock
	{
		public static readonly DependencyProperty HighlightPatternProperty = DependencyProperty.RegisterAttached("HighlightString", typeof(string), typeof(HighlightableTextBlock), new PropertyMetadata(delegate(DependencyObject s, DependencyPropertyChangedEventArgs e)
		{
			HighlightableTextBlock highlightableTextBlock = s as HighlightableTextBlock;
			highlightableTextBlock.ApplySearchHighlighting(highlightableTextBlock.HighlightString);
		}));

		public string HighlightString
		{
			get
			{
				return (string)GetValue(HighlightPatternProperty);
			}
			set
			{
				SetValue(HighlightPatternProperty, value);
			}
		}
	}
}
