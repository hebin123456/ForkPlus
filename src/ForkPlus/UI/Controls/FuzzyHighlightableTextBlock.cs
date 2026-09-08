using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	internal class FuzzyHighlightableTextBlock : TextBlock
	{
		public static readonly global::Avalonia.StyledProperty<string> FuzzySearchStringProperty =
    global::Avalonia.AvaloniaProperty.RegisterAttached<FuzzyHighlightableTextBlock, global::Avalonia.AvaloniaObject, string>("FuzzySearchString");

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
