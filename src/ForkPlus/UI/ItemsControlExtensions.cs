// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.* → using Avalonia.*
// - DependencyObject → AvaloniaObject（泛型约束）
// - VisualTreeHelper.HitTest → InputHitTest（Avalonia IInputElement 方法，返回 IInputElement）
// - VisualTreeHelper.GetParent → GetVisualParent()（Avalonia.VisualTree 扩展方法）
// - HitTestResult.VisualHit → InputHitTest 直接返回 IInputElement（已在视觉树中）
// - Keyboard.Focus(element) → element.Focus()（Avalonia Control.Focus 方法）
// - ItemContainerGenerator.ItemFromContainer → Avalonia ItemContainerGenerator.ItemFromContainer（API 兼容）
// - Selector/ItemsControl → Avalonia.Controls.Primitives.Selector / Avalonia.Controls.ItemsControl
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace ForkPlus.UI
{
	public static class ItemsControlExtensions
	{
		public static object GetObjectAtPoint<ItemContainer>(this ItemsControl control, Point p) where ItemContainer : AvaloniaObject
		{
			ItemContainer containerAtPoint = control.GetContainerAtPoint<ItemContainer>(p);
			if (containerAtPoint == null)
			{
				return null;
			}
			return control.ItemContainerGenerator.ItemFromContainer(containerAtPoint);
		}

		public static ItemContainer GetContainerAtPoint<ItemContainer>(this ItemsControl control, Point p) where ItemContainer : AvaloniaObject
		{
			// 阶段 4.5：WPF VisualTreeHelper.HitTest(control, p) 返回 HitTestResult.VisualHit（DependencyObject）
			// → Avalonia IInputElement.InputHitTest(p) 返回 IInputElement（视觉树中的命中元素）。
			IInputElement hitTestResult = (control as IInputElement)?.InputHitTest(p);
			if (hitTestResult == null)
			{
				return null;
			}
			AvaloniaObject dependencyObject = hitTestResult as AvaloniaObject;
			// 阶段 4.5：WPF VisualTreeHelper.GetParent → Avalonia GetVisualParent()（沿视觉树向上）。
			while (dependencyObject?.GetVisualParent() != null && !(dependencyObject is ItemContainer))
			{
				dependencyObject = dependencyObject.GetVisualParent();
			}
			return dependencyObject as ItemContainer;
		}

		public static void FocusSelectedItem(this Selector control)
		{
			// 阶段 4.5：WPF ItemContainerGenerator.ContainerFromIndex → Avalonia ItemsControl.ContainerFromIndex。
			if (control.SelectedIndex >= 0 && control.ContainerFromIndex(control.SelectedIndex) is IInputElement element)
			{
				// 阶段 4.5：WPF Keyboard.Focus(element) → Avalonia InputElement.Focus()。
				element.Focus();
			}
		}
	}
}
