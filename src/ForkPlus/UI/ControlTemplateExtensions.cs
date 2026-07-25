using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
// 阶段 5：Avalonia 的 ControlTemplate 具体类在 Avalonia.Markup.Xaml.Templates
// （Avalonia.Controls.Templates 仅含 IControlTemplate/IDataTemplate 等接口）。
using Avalonia.Markup.Xaml.Templates;

namespace ForkPlus.UI
{
	public static class ControlTemplateExtensions
	{
		// 阶段 4 里程碑 4.7-a：WPF ControlTemplate.FindName(name, templatedParent) →
		// Avalonia 无直接等价物；标准做法是在 OnApplyTemplate 中用 e.NameScope.Find
		// 缓存模板部件。本帮助器仅在模板已应用后通过 INameScope 查找。
		// 阶段 5：TemplatedControl.GetTemplateChildren() 是 internal API（Avalonia 11.3），
		// 外部不可用；改用 TemplatedControl 找到的 INameScope（通过 NameScope 属性）。
		public static bool TryFindName<T>(this ControlTemplate source, string name, TemplatedControl templatedParent, out T match) where T : class
		{
			match = null;
			INameScope nameScope = (templatedParent as INameScope) ?? NameScope.GetNameScope(templatedParent);
			if (nameScope == null)
			{
				return false;
			}
			match = nameScope.Find(name) as T;
			return match != null;
		}
	}
}
