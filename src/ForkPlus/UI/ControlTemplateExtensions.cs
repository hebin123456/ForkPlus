using System.Windows;
using System.Windows.Controls;

namespace ForkPlus.UI
{
	public static class ControlTemplateExtensions
	{
		public static bool TryFindName<T>(this ControlTemplate source, string name, FrameworkElement templatedParent, out T match) where T : class
		{
			object obj = source.FindName(name, templatedParent);
			match = obj as T;
			if (match != null)
			{
				return true;
			}
			return false;
		}
	}
}
