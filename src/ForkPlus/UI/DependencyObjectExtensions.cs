using Avalonia;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI
{
	public static class DependencyObjectExtensions
	{
		[Null]
		public static T GetParent<T>(this global::Avalonia.Visual _this) where T : global::Avalonia.AvaloniaObject
		{
			// Migration note：WPF DependencyObject 可视树遍历 → Avalonia Visual（GetVisualParent 需要 Visual）。
			global::Avalonia.Visual dependencyObject = _this;
			while (dependencyObject != null && !(dependencyObject is T))
			{
				dependencyObject = global::Avalonia.VisualTree.VisualExtensions.GetVisualParent(dependencyObject);
			}
			return dependencyObject as T;
		}
	}
}
