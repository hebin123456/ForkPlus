using ForkPlus.UI.Helpers;
using System.Windows;

namespace ForkPlus.UI.Helpers
{
	public static class FrameworkElementExtension
	{
		[Null]
		public static T Parent<T>(this FrameworkElement frameworkElement) where T : class
		{
			return frameworkElement.Parent as T;
		}
	}
}
