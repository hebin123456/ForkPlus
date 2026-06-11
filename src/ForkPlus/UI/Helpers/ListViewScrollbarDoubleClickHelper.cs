using ForkPlus.UI.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ForkPlus.UI.Helpers
{
	public static class ListViewScrollbarDoubleClickHelper
	{
		public static bool IsClickedOnScrollbar(this MouseButtonEventArgs args)
		{
			DependencyObject dependencyObject = args.OriginalSource as DependencyObject;
			while (dependencyObject != null && !(dependencyObject is ListViewItem))
			{
				dependencyObject = ((!(dependencyObject is Run)) ? VisualTreeHelper.GetParent(dependencyObject) : (dependencyObject as Run).Parent);
			}
			if (dependencyObject == null)
			{
				return true;
			}
			return false;
		}
	}
}
