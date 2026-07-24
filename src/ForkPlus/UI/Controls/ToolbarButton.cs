using Avalonia;
using Avalonia.Controls;

namespace ForkPlus.UI.Controls
{
	public class ToolbarButton : Button
	{
		public static readonly StyledProperty<string> TitleProperty =
			AvaloniaProperty.Register<ToolbarButton, string>(nameof(Title));

		public string Title
		{
			get => GetValue(TitleProperty);
			set => SetValue(TitleProperty, value);
		}
	}
}
