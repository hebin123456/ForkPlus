using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ForkPlus.UI
{
	public static class ItemsControlExtensions
	{
		public static object GetObjectAtPoint<ItemContainer>(this ItemsControl control, Point p) where ItemContainer : DependencyObject
		{
			ItemContainer containerAtPoint = control.GetContainerAtPoint<ItemContainer>(p);
			if (containerAtPoint == null)
			{
				return null;
			}
			return control.ItemContainerGenerator.ItemFromContainer(containerAtPoint);
		}

		public static ItemContainer GetContainerAtPoint<ItemContainer>(this ItemsControl control, Point p) where ItemContainer : DependencyObject
		{
			HitTestResult hitTestResult = VisualTreeHelper.HitTest(control, p);
			if (hitTestResult == null)
			{
				return null;
			}
			DependencyObject dependencyObject = hitTestResult.VisualHit;
			while (VisualTreeHelper.GetParent(dependencyObject) != null && !(dependencyObject is ItemContainer))
			{
				dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
			}
			return dependencyObject as ItemContainer;
		}

		public static void FocusSelectedItem(this Selector control)
		{
			if (control.SelectedIndex >= 0 && control.ItemContainerGenerator.ContainerFromIndex(control.SelectedIndex) is IInputElement element)
			{
				Keyboard.Focus(element);
			}
		}
	}
}
