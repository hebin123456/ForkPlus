using Avalonia;
using Avalonia.VisualTree;

namespace ForkPlus.UI
{
	// 阶段 4.5：WPF DependencyObject + VisualTreeHelper.GetParent
	// → Avalonia AvaloniaObject + IVisual.GetVisualParent。
	// Avalonia 没有 LogicalTreeHelper，逻辑父级通过 ILogical 接口获取；
	// 这里使用 GetVisualParent 满足 GetParent<T> 在控件树上向上查找的语义。
	public static class DependencyObjectExtensions
	{
		[Null]
		public static T GetParent<T>(this AvaloniaObject _this) where T : AvaloniaObject
		{
			AvaloniaObject current = _this;
			while (current != null && !(current is T))
			{
				current = (current as IVisual)?.GetVisualParent() as AvaloniaObject;
			}
			return current as T;
		}
	}
}
