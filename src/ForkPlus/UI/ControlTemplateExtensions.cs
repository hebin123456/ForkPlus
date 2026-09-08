using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace ForkPlus.UI
{
	public static class ControlTemplateExtensions
	{
		/// <summary>
		/// Migration note：WPF ControlTemplate.FindName(name, templatedParent) 在 Avalonia 无直接对应。
		/// 近似实现：模板应用后控件经 NameScope.GetNameScope 持有模板命名域，先查命名域；
		/// 查不到再沿可视树按 Control.Name 匹配（模板部件均已实例化时两者等价）。
		/// </summary>
		public static bool TryFindName<T>(this global::Avalonia.Controls.Templates.IControlTemplate source, string name, global::Avalonia.Controls.Control templatedParent, out T match) where T : class
		{
			object obj = null;
			try
			{
				obj = global::Avalonia.Controls.NameScope.GetNameScope(templatedParent)?.Find(name);
			}
			catch { }
			if (obj == null && templatedParent != null)
			{
				obj = templatedParent.GetVisualDescendants()
					.OfType<global::Avalonia.Controls.Control>()
					.FirstOrDefault(c => c.Name == name);
			}
			match = obj as T;
			if (match != null)
			{
				return true;
			}
			return false;
		}
	}
}
