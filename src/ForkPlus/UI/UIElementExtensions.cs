using Avalonia.Controls;

namespace ForkPlus.UI
{
	// 阶段 4.5：WPF UIElement.Visibility (Visible/Hidden/Collapsed) → Avalonia Control.IsVisible (bool)。
	// Avalonia 没有 Visibility.Hidden（保留布局空间），所有 Hide 调用退化为 Collapsed 行为。
	// 现有调用方多用于切换 busy/fallback 控件可见性，Collapsed 更符合预期。
	public static class UIElementExtensions
	{
		public static void Show(this Control element)
		{
			element.IsVisible = true;
		}

		public static void Collapse(this Control element)
		{
			element.IsVisible = false;
		}

		public static void Hide(this Control element)
		{
			element.IsVisible = false;
		}

		public static void Hide(this Control element, bool hide)
		{
			element.IsVisible = !hide;
		}

		public static void Disable(this Control element)
		{
			element.IsEnabled = false;
		}

		public static void Enable(this Control element)
		{
			element.IsEnabled = true;
		}
	}
}
