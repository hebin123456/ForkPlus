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
			return ItemFromContainer(control, containerAtPoint);
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
			// 阶段 4.5：GetVisualParent() 是 Avalonia.VisualTree 扩展方法，接收者为 Visual（不是 AvaloniaObject）。
			// IInputElement 在 Avalonia 中实现者均为 Visual，直接转 Visual。
			Visual dependencyObject = hitTestResult as Visual;
			// 阶段 4.5：WPF VisualTreeHelper.GetParent → Avalonia GetVisualParent()（沿视觉树向上）。
			while (dependencyObject?.GetVisualParent() != null && !(dependencyObject is ItemContainer))
			{
				dependencyObject = dependencyObject.GetVisualParent();
			}
			return dependencyObject as ItemContainer;
		}

		public static void FocusSelectedItem(this SelectingItemsControl control)
		{
			// 阶段 4.5：WPF ItemContainerGenerator.ContainerFromIndex → Avalonia ItemsControl.ContainerFromIndex。
			if (control.SelectedIndex >= 0 && control.ContainerFromIndex(control.SelectedIndex) is IInputElement element)
			{
				// 阶段 4.5：WPF Keyboard.Focus(element) → Avalonia InputElement.Focus()。
				element.Focus();
			}
		}

		// 阶段 4.5：Avalonia 11.3 ItemContainerGenerator 无 ItemFromContainer 方法。
		// 通过遍历 Items + ContainerFromItem 反向查找数据项。
		// 阶段 5：Avalonia ItemContainerGenerator 也无 ContainerFromItem，改用 ItemsControl.ContainerFromItem（直接在 ItemsControl 上调用）。
		private static object ItemFromContainer(ItemsControl itemsControl, object container)
		{
			foreach (var item in itemsControl.Items)
			{
				if (itemsControl.ContainerFromItem(item) == container)
				{
					return item;
				}
			}
			return null;
		}
	}
}
