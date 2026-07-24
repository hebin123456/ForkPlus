using Avalonia;
using Avalonia.Controls;

namespace ForkPlus.UI.Controls
{
	public class ToolbarDropDownButton : DropDownButton
	{
		public static readonly StyledProperty<string> TitleProperty =
			AvaloniaProperty.Register<ToolbarDropDownButton, string>(nameof(Title));

		public static readonly StyledProperty<bool> IsArrowVisibleProperty =
			AvaloniaProperty.Register<ToolbarDropDownButton, bool>(nameof(IsArrowVisible), true);

		public string Title
		{
			get => GetValue(TitleProperty);
			set => SetValue(TitleProperty, value);
		}

		public bool IsArrowVisible
		{
			get => GetValue(IsArrowVisibleProperty);
			set => SetValue(IsArrowVisibleProperty, value);
		}
	}
}
