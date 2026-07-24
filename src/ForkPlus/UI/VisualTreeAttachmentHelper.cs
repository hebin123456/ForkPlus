using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace ForkPlus.UI
{
	// 阶段 4.5：WPF DependencyObject/UIElement/FrameworkElement/FrameworkContentElement/Visual/Visual3D
	// → Avalonia AvaloniaObject/Control/Visual。
	// WPF LogicalTreeHelper.GetParent + VisualTreeHelper.GetParent
	// → Avalonia ILogical.GetLogicalParent() + IVisual.GetVisualParent()。
	// Avalonia 没有 FrameworkContentElement 概念（WPF 流文档模型），Describe 简化只判断 Control.Name。
	internal static class VisualTreeAttachmentHelper
	{
		public static bool TryAddChild(Panel panel, Control child, string targetDescription)
		{
			if (panel == null)
			{
				return false;
			}
			if (child == null)
			{
				return true;
			}
			if (!PrepareForNewParent(child, targetDescription))
			{
				return false;
			}
			panel.Children.Add(child);
			return true;
		}

		public static bool TrySetChild(Decorator decorator, Control child, string targetDescription)
		{
			if (decorator == null)
			{
				return false;
			}
			if (child != null && !PrepareForNewParent(child, targetDescription))
			{
				return false;
			}
			decorator.Child = child;
			return true;
		}

		public static bool TrySetPopupChild(Popup popup, Control child, string targetDescription)
		{
			if (popup == null)
			{
				return false;
			}
			if (child != null && !PrepareForNewParent(child, targetDescription))
			{
				return false;
			}
			popup.Child = child;
			return true;
		}

		public static bool TrySetContent(ContentControl contentControl, object content, string targetDescription)
		{
			if (contentControl == null)
			{
				return false;
			}
			if (content is AvaloniaObject dependencyObject && !PrepareForNewParent(dependencyObject, targetDescription))
			{
				return false;
			}
			contentControl.Content = content;
			return true;
		}

		public static bool PrepareForNewParent(AvaloniaObject child, string targetDescription)
		{
			if (child == null)
			{
				return true;
			}
			AvaloniaObject parent = GetParent(child);
			if (parent == null)
			{
				return true;
			}
			if (!DetachFromParent(child, parent))
			{
				if (GetParent(child) == null)
				{
					return true;
				}
				Log.Warn("Cannot detach " + Describe(child) + " from " + Describe(parent) + " before attaching to " + targetDescription + ".");
				return false;
			}
			AvaloniaObject parent2 = GetParent(child);
			if (parent2 != null)
			{
				Log.Warn("Detached " + Describe(child) + " from " + Describe(parent) + " but it is still parented by " + Describe(parent2) + " before attaching to " + targetDescription + ".");
				return false;
			}
			return true;
		}

		public static string Describe(AvaloniaObject item)
		{
			if (item == null)
			{
				return "null";
			}
			if (item is Control control && !string.IsNullOrEmpty(control.Name))
			{
				return item.GetType().Name + "('" + control.Name + "')";
			}
			return item.GetType().Name;
		}

		private static AvaloniaObject GetParent(AvaloniaObject child)
		{
			// 阶段 4.5：WPF LogicalTreeHelper.GetParent + VisualTreeHelper.GetParent
			// → Avalonia ILogical.GetLogicalParent + IVisual.GetVisualParent。
			if (child is ILogical logical)
			{
				ILogical logicalParent = logical.GetLogicalParent();
				if (logicalParent is AvaloniaObject logicalParentObj)
				{
					return logicalParentObj;
				}
			}
			if (child is IVisual visual)
			{
				IVisual visualParent = visual.GetVisualParent();
				if (visualParent is AvaloniaObject visualParentObj)
				{
					return visualParentObj;
				}
			}
			return null;
		}

		private static bool DetachFromParent(AvaloniaObject child, AvaloniaObject parent)
		{
			if (parent is Popup popup && child is Control uIElement && ReferenceEquals(popup.Child, uIElement))
			{
				popup.Child = null;
				return true;
			}
			if (parent is Panel panel && child is Control uIElement2 && panel.Children.Contains(uIElement2))
			{
				panel.Children.Remove(uIElement2);
				return true;
			}
			if (parent is Decorator decorator && child is Control uIElement3 && ReferenceEquals(decorator.Child, uIElement3))
			{
				decorator.Child = null;
				return true;
			}
			if (parent is HeaderedContentControl headeredContentControl && ReferenceEquals(headeredContentControl.Header, child))
			{
				headeredContentControl.Header = null;
				return true;
			}
			if (parent is HeaderedItemsControl headeredItemsControl && ReferenceEquals(headeredItemsControl.Header, child))
			{
				headeredItemsControl.Header = null;
				return true;
			}
			if (parent is ContentControl contentControl && ReferenceEquals(contentControl.Content, child))
			{
				contentControl.Content = null;
				return true;
			}
			if (parent is ContentPresenter contentPresenter && ReferenceEquals(contentPresenter.Content, child))
			{
				contentPresenter.Content = null;
				return true;
			}
			if (parent is ItemsControl itemsControl && itemsControl.Items.Contains(child))
			{
				itemsControl.Items.Remove(child);
				return true;
			}
			return false;
		}
	}
}
