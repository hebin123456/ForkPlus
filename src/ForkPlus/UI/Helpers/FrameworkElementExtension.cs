using Avalonia.Controls;

namespace ForkPlus.UI.Helpers
{
	/// <summary>
	/// 阶段 4.5：从 WPF <c>FrameworkElement</c> 迁移到 Avalonia <see cref="Control"/>。
	/// Avalonia <see cref="StyledElement.Parent"/> 返回逻辑父级（等价 WPF FrameworkElement.Parent）。
	/// 类名保留旧名以避免改动调用点。
	/// </summary>
	public static class FrameworkElementExtension
	{
		[Null]
		public static T Parent<T>(this Control control) where T : class
		{
			return control.Parent as T;
		}
	}
}
