using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class ToolbarButton : Button
	{
		public static readonly global::Avalonia.StyledProperty<string> TitleProperty =
    global::Avalonia.AvaloniaProperty.Register<ToolbarButton, string>("Title", null);

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
	}
}
