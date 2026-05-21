using System.Windows;
using System.Windows.Controls;

namespace ForkPlus.UI.Controls
{
	internal class FuzzyHighlightableTextBlock : TextBlock
	{
		public static readonly DependencyProperty FuzzySearchStringProperty = DependencyProperty.RegisterAttached("FuzzySearchString", typeof(string), typeof(FuzzyHighlightableTextBlock), new PropertyMetadata(delegate(DependencyObject s, DependencyPropertyChangedEventArgs e)
		{
			FuzzyHighlightableTextBlock obj = s as FuzzyHighlightableTextBlock;
			obj.ApplyFuzzyHighlighting(obj.FuzzySearchString);
		}));

		public string FuzzySearchString
		{
			get
			{
				return (string)GetValue(FuzzySearchStringProperty);
			}
			set
			{
				SetValue(FuzzySearchStringProperty, value);
			}
		}
	}
}
