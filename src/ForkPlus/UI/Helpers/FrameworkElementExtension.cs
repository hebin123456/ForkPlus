using ForkPlus.UI.Helpers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Helpers
{
	public static class FrameworkElementExtension
	{
		[Null]
		public static T Parent<T>(this global::Avalonia.Controls.Control frameworkElement) where T : class
		{
			return frameworkElement.Parent as T;
		}
	}
}
