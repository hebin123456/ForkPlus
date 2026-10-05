using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;

namespace ForkPlus.Plugins
{
	/// <summary>
	/// 插件侧的 WPF→Avalonia 兼容小件（v5.0.0 插件化架构）。
	/// 仅收录 Hex/图片视图需要的纯 Avalonia 逻辑（与主工程 WpfCompat 同实现拆出）；
	/// 有全局状态的部分（键盘跟踪、剪贴板、设置持久化）不走这里，
	/// 经 PluginEnvironment 的宿主注入委托（见宿主 PluginEnvironmentBridge）。
	/// </summary>
	internal static class PluginCompat
	{
		/// <summary>对象初始化器链式辅助：new Button { ... }.WithTip("xx")。</summary>
		public static T WithTip<T>(this T control, object tip) where T : global::Avalonia.Visual
		{
			if (control is Control c)
			{
				ToolTip.SetTip(c, tip);
			}
			return control;
		}

		/// <summary>WPF Application.Current.TryFindResource(key)（Application 不是 StyledElement，单独适配，与主工程 WpfCompat 同实现）。</summary>
	public static object TryFindResource(this Avalonia.Application app, object key)
	{
		if (app == null)
		{
			return null;
		}
		return app.TryGetResource(key, app.ActualThemeVariant ?? ThemeVariant.Default, out var value) ? value : null;
	}

	/// <summary>
	/// WPF control.Style = s 适配（Avalonia Styles 只读集合）。
	/// 主题资源（x:Key 的 Style）迁移后全部是 ControlTheme，必须挂 TemplatedControl.Theme
	///（等价 XAML 里 Theme="{...}"），塞进 Styles 集合不生效。
	/// </summary>
	public static void SetStyle(StyledElement element, object style)
		{
			element.Styles.Clear();
			switch (style)
			{
				case ControlTheme ct:
					if (element is TemplatedControl templatedControl)
					{
						templatedControl.Theme = ct;
					}
					break;
				case Style s:
					element.Styles.Add(s);
					break;
				case System.Collections.IEnumerable en and not string:
					{
						foreach (object st in en)
						{
							if (st is Style s2)
							{
								element.Styles.Add(s2);
							}
						}
						break;
					}
			}
		}
	}

	/// <summary>WPF ScrollViewer.ScrollTo*Offset 扩展（Avalonia 经 Offset 属性设置；AvaloniaEdit 12.x 的 ScrollTo*Offset 是空操作）。</summary>
	internal static class PluginScrollCompat
	{
		public static void ScrollToVerticalOffsetCompat(this ScrollViewer sv, double offset)
		{
			if (sv == null)
			{
				return;
			}
			sv.Offset = sv.Offset.WithY(offset);
		}

		public static void ScrollToHorizontalOffsetCompat(this ScrollViewer sv, double offset)
		{
			if (sv == null)
			{
				return;
			}
			sv.Offset = sv.Offset.WithX(offset);
		}

		/// <summary>滚动 TextEditor（经模板 PART_ScrollViewer.Offset，修复 AvaloniaEdit 空操作）。</summary>
		public static void ScrollToVerticalOffsetCompat(this TextEditor editor, double offset)
		{
			ScrollViewer sv = FindEditorScrollViewer(editor);
			if (sv == null)
			{
				return;
			}
			sv.Offset = sv.Offset.WithY(offset);
		}

		/// <summary>滚动 TextEditor（经模板 PART_ScrollViewer.Offset，修复 AvaloniaEdit 空操作）。</summary>
		public static void ScrollToHorizontalOffsetCompat(this TextEditor editor, double offset)
		{
			ScrollViewer sv = FindEditorScrollViewer(editor);
			if (sv == null)
			{
				return;
			}
			sv.Offset = sv.Offset.WithX(offset);
		}

		// TextEditor.ScrollViewer 是 AvaloniaEdit internal，从模板部件/可视树里找
		// PART_ScrollViewer（找不到具名的则退回第一个 ScrollViewer，兼容自定义模板）。
		private static ScrollViewer FindEditorScrollViewer(TextEditor editor)
		{
			if (editor == null)
			{
				return null;
			}
			ScrollViewer named = editor.GetVisualDescendants().OfType<ScrollViewer>()
				.FirstOrDefault((ScrollViewer x) => x.Name == "PART_ScrollViewer");
			if (named != null)
			{
				return named;
			}
			return editor.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
		}
	}

	/// <summary>TextEditor 滚动区判定（AvaloniaEdit TextView 实现 IScrollable；WPF 经 IScrollInfo 读滚动区）。</summary>
	internal static class PluginTextEditorOffsetExtensions
	{
		public static bool IsVerticalOffsetWithinDocumentArea(this TextEditor textEditor, double offset)
		{
			TextView textView = textEditor.TextArea.TextView;
			double extentHeight = ((IScrollable)textView).Extent.Height;
			double viewportHeight = ((IScrollable)textView).Viewport.Height;
			if (offset + viewportHeight > extentHeight)
			{
				return false;
			}
			return true;
		}

		public static bool IsHorizontalOffsetWithinDocumentArea(this TextEditor textEditor, double offset)
		{
			TextView textView = textEditor.TextArea.TextView;
			double extentWidth = ((IScrollable)textView).Extent.Width;
			double viewportWidth = ((IScrollable)textView).Viewport.Width;
			if (offset + viewportWidth > extentWidth)
			{
				return false;
			}
			return true;
		}
	}
}
