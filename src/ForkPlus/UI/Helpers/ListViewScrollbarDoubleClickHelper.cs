using ForkPlus.UI.Helpers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Helpers
{
	public static class ListViewScrollbarDoubleClickHelper
	{
		public static bool IsClickedOnScrollbar(this global::Avalonia.Input.PointerPressedEventArgs args)
		{
			return IsSourceInsideListBoxItem(args.Source);
		}

		// Migration note：WPF MouseButtonEventArgs（双击/单击）→ Avalonia TappedEventArgs 重载。
		public static bool IsClickedOnScrollbar(this global::Avalonia.Input.TappedEventArgs args)
		{
			return IsSourceInsideListBoxItem(args.Source);
		}

		private static bool IsSourceInsideListBoxItem(object source)
		{
			// Migration note：WPF DependencyObject 可视树遍历 → Avalonia Visual。
			// WPF 里 Run（Inline）不是 Visual 才需走 Run.Parent 特殊分支；Avalonia 指针事件源必为 Visual，直接向上遍历即可。
			global::Avalonia.Visual dependencyObject = source as global::Avalonia.Visual;
			while (dependencyObject != null && !(dependencyObject is global::Avalonia.Controls.ListBoxItem))
			{
				dependencyObject = global::Avalonia.VisualTree.VisualExtensions.GetVisualParent(dependencyObject);
			}
			return dependencyObject == null;
		}
	}
}
