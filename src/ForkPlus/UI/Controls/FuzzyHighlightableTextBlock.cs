using Avalonia;
using Avalonia.Controls;

namespace ForkPlus.UI.Controls
{
	internal class FuzzyHighlightableTextBlock : TextBlock
	{
		// 阶段 4.5：WPF DependencyProperty.RegisterAttached + PropertyMetadata 回调
		// → Avalonia StyledProperty + OnPropertyChanged override。
		public static readonly StyledProperty<string> FuzzySearchStringProperty =
			AvaloniaProperty.Register<FuzzyHighlightableTextBlock, string>(nameof(FuzzySearchString));

		public string FuzzySearchString
		{
			get => GetValue(FuzzySearchStringProperty);
			set => SetValue(FuzzySearchStringProperty, value);
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == FuzzySearchStringProperty)
			{
				this.ApplyFuzzyHighlighting(FuzzySearchString);
			}
		}
	}
}
