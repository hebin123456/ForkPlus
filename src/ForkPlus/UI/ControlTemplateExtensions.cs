using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;

namespace ForkPlus.UI
{
	public static class ControlTemplateExtensions
	{
		// 阶段 4 里程碑 4.7-a：WPF ControlTemplate.FindName(name, templatedParent) →
		// Avalonia TemplatedControl.GetTemplateChildren() 遍历按 Name 匹配。Avalonia 无
		// ControlTemplate.FindName 等价物；标准做法是在 OnApplyTemplate 中用 e.NameScope.Find
		// 缓存模板部件，此处保留帮助器以最小化调用方改动。
		public static bool TryFindName<T>(this ControlTemplate source, string name, TemplatedControl templatedParent, out T match) where T : class
		{
			match = null;
			foreach (Control child in templatedParent.GetTemplateChildren())
			{
				if (child.Name == name)
				{
					match = child as T;
					return match != null;
				}
			}
			return false;
		}
	}
}
