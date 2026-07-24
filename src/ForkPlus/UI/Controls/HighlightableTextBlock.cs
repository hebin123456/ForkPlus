using Avalonia;
using Avalonia.Controls;

namespace ForkPlus.UI.Controls
{
	internal class HighlightableTextBlock : TextBlock
	{
		// 阶段 4.5：WPF DependencyProperty.RegisterAttached + PropertyMetadata 回调
		// → Avalonia StyledProperty + OnPropertyChanged override。
		public static readonly StyledProperty<string> HighlightStringProperty =
			AvaloniaProperty.Register<HighlightableTextBlock, string>(nameof(HighlightString));

		public string HighlightString
		{
			get => GetValue(HighlightStringProperty);
			set => SetValue(HighlightStringProperty, value);
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == HighlightStringProperty)
			{
				this.ApplySearchHighlighting(HighlightString);
			}
		}
	}
}
