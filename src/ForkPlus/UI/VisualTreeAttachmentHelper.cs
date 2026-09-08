using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI
{
	internal static class VisualTreeAttachmentHelper
	{
		public static bool TryAddChild(Panel panel, global::Avalonia.Input.InputElement child, string targetDescription)
		{
			if (panel == null)
			{
				return false;
			}
			if (child == null)
			{
				return true;
			}
			// Migration note：Avalonia 的 Panel.Children 只接受 Control（WPF 是 UIElement/InputElement）；
			// 方法签名沿用 InputElement（调用方均传 Control），非 Control 输入无法挂视觉树，返回 false。
			if (!(child is global::Avalonia.Controls.Control controlChild))
			{
				return false;
			}
			if (!PrepareForNewParent(child, targetDescription))
			{
				return false;
			}
			panel.Children.Add(controlChild);
			return true;
		}

		public static bool TrySetChild(Decorator decorator, global::Avalonia.Input.InputElement child, string targetDescription)
		{
			if (decorator == null)
			{
				return false;
			}
			if (child != null && !PrepareForNewParent(child, targetDescription))
			{
				return false;
			}
			// Migration note：Avalonia Decorator.Child 类型是 Control（WPF 是 UIElement）；
			// 用 as 降级转换，child 为 null 或非 Control 时置 null（调用方实际都传 Control）。
			decorator.Child = child as global::Avalonia.Controls.Control;
			return true;
		}

		public static bool TrySetPopupChild(Popup popup, global::Avalonia.Input.InputElement child, string targetDescription)
		{
			if (popup == null)
			{
				return false;
			}
			if (child != null && !PrepareForNewParent(child, targetDescription))
			{
				return false;
			}
			// Migration note：Avalonia Popup.Child 类型是 Control（WPF 是 UIElement）；
			// 用 as 降级转换，child 为 null 或非 Control 时置 null（调用方实际都传 Control）。
			popup.Child = child as global::Avalonia.Controls.Control;
			return true;
		}

		public static bool TrySetContent(ContentControl contentControl, object content, string targetDescription)
		{
			if (contentControl == null)
			{
				return false;
			}
			if (content is global::Avalonia.AvaloniaObject dependencyObject && !PrepareForNewParent(dependencyObject, targetDescription))
			{
				return false;
			}
			contentControl.Content = content;
			return true;
		}

		public static bool PrepareForNewParent(global::Avalonia.AvaloniaObject child, string targetDescription)
		{
			if (child == null)
			{
				return true;
			}
			global::Avalonia.AvaloniaObject parent = GetParent(child);
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
			global::Avalonia.AvaloniaObject parent2 = GetParent(child);
			if (parent2 != null)
			{
				Log.Warn("Detached " + Describe(child) + " from " + Describe(parent) + " but it is still parented by " + Describe(parent2) + " before attaching to " + targetDescription + ".");
				return false;
			}
			return true;
		}

		public static string Describe(global::Avalonia.AvaloniaObject item)
		{
			if (item == null)
			{
				return "null";
			}
			if (item is global::Avalonia.Controls.Control frameworkElement && !string.IsNullOrEmpty(frameworkElement.Name))
			{
				return item.GetType().Name + "('" + frameworkElement.Name + "')";
			}
			// Migration note：WPF FrameworkContentElement 分支删除——Avalonia 没有 ContentElement 体系，
			// 可视树成员全部是 Control，上面的 Control 分支已覆盖命名描述。
			return item.GetType().Name;
		}

		private static global::Avalonia.AvaloniaObject GetParent(global::Avalonia.AvaloniaObject child)
		{
			// Migration note：WPF 同时查逻辑树（LogicalTreeHelper）与视觉树（Visual/Visual3D）；
			// Avalonia 逻辑父 = StyledElement.Parent（经 WpfCompat LogicalTreeHelper 垫片），
			// 视觉父 = VisualExtensions.GetVisualParent(Visual)（Avalonia 无 Visual3D，只有一棵视觉树）。
			// 两个 API 都要求具体类型，先做模式匹配再调用。
			if (child is global::Avalonia.StyledElement styledElement)
			{
				global::Avalonia.AvaloniaObject parent = LogicalTreeHelper.GetParent(styledElement);
				if (parent != null)
				{
					return parent;
				}
			}
			if (child is global::Avalonia.Visual visual)
			{
				return global::Avalonia.VisualTree.VisualExtensions.GetVisualParent(visual);
			}
			return null;
		}

		private static bool DetachFromParent(global::Avalonia.AvaloniaObject child, global::Avalonia.AvaloniaObject parent)
		{
			if (parent is Popup popup && child is global::Avalonia.Input.InputElement uIElement && ReferenceEquals(popup.Child, uIElement))
			{
				popup.Child = null;
				return true;
			}
			if (parent is Panel panel && child is global::Avalonia.Controls.Control controlChild && panel.Children.Contains(controlChild))
		{
			// Migration note：Panel.Children 是 IList<Control>（WPF 是 UIElement 集合），
			// Contains/Remove 均要求 Control，故此处模式变量直接用 Control（原 InputElement 会 CS1503）。
			panel.Children.Remove(controlChild);
			return true;
		}
			if (parent is Decorator decorator && child is global::Avalonia.Input.InputElement uIElement3 && ReferenceEquals(decorator.Child, uIElement3))
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
