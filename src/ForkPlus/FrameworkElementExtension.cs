using System.Windows;

namespace ForkPlus
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
