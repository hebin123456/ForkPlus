using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI
{
	public static class UIElementExtensions
	{
		public static void Show(this global::Avalonia.Input.InputElement element)
		{
			element.IsVisible = true;
		}

		public static void Collapse(this global::Avalonia.Input.InputElement element)
		{
			element.IsVisible = false;
		}

		public static void Hide(this global::Avalonia.Input.InputElement element)
		{
			element.IsVisible = false;
		}

		public static void Hide(this global::Avalonia.Input.InputElement element, bool hide)
		{
			element.IsVisible = (hide ? false : true);
		}

		public static void Disable(this global::Avalonia.Input.InputElement element)
		{
			element.IsEnabled = false;
		}

		public static void Enable(this global::Avalonia.Input.InputElement element)
		{
			element.IsEnabled = true;
		}
	}
}
