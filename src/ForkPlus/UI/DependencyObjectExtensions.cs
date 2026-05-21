using System.Windows;
using System.Windows.Media;

namespace ForkPlus.UI
{
	public static class DependencyObjectExtensions
	{
		[Null]
		public static T GetParent<T>(this DependencyObject _this) where T : DependencyObject
		{
			DependencyObject dependencyObject = _this;
			while (dependencyObject != null && !(dependencyObject is T))
			{
				dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
			}
			return dependencyObject as T;
		}
	}
}
