using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace ForkPlus.UI.Helpers
{
	// 阶段 4.5：WPF System.Windows.* → Avalonia.*。
	// WPF MouseButtonEventArgs → Avalonia PointerPressedEventArgs。
	// WPF args.OriginalSource → Avalonia args.Source。
	// WPF VisualTreeHelper.GetParent → Avalonia GetVisualParent()。
	// WPF DependencyObject → Avalonia Visual（视觉树节点）。
	// WPF Run.Parent 特殊处理移除（Avalonia Inline 不在视觉树中，Source 通常是 TextBlock）。
	public static class ListViewScrollbarDoubleClickHelper
	{
		public static bool IsClickedOnScrollbar(this PointerPressedEventArgs args)
		{
			Visual visual = args.Source as Visual;
			while (visual != null && !(visual is ListBoxItem))
			{
				visual = visual.GetVisualParent();
			}
			if (visual == null)
			{
				return true;
			}
			return false;
		}
	}
}
