using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class ToolbarDropDownButton : DropDownButton
	{
		public static readonly global::Avalonia.StyledProperty<string> TitleProperty =
    global::Avalonia.AvaloniaProperty.Register<ToolbarDropDownButton, string>("Title", null);

		public static readonly global::Avalonia.StyledProperty<bool> IsArrowVisibleProperty =
    global::Avalonia.AvaloniaProperty.Register<ToolbarDropDownButton, bool>("IsArrowVisible", true);

		public string Title
		{
			get
			{
				return (string)GetValue(TitleProperty);
			}
			set
			{
				SetValue(TitleProperty, value);
			}
		}

		public bool IsArrowVisible
		{
			get
			{
				return (bool)GetValue(IsArrowVisibleProperty);
			}
			set
			{
				SetValue(IsArrowVisibleProperty, value);
			}
		}
	}
}
