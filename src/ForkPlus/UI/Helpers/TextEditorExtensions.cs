using AvaloniaEdit;
using AvaloniaEdit.Rendering;

namespace ForkPlus.UI.Helpers
{
	internal static class TextEditorExtensions
	{
		// 阶段 4 里程碑 4.7-a：System.Windows.Controls.Primitives.IScrollInfo → Avalonia.Controls.Primitives.IScrollInfo。
		// 阶段 5：Avalonia 11.3 已移除 IScrollInfo 接口，TextView 不再实现该接口。
		// 改用 TextView.Bounds.Height（视口高度）和 TextView.Bounds.Height + VerticalOffset（文档总高度）近似判断。
		// 注：AvaloniaEdit TextView 的滚动范围由内部 ScrollInfo 管理，此处用 Bounds 近似（阶段 6 可改为访问内部 ScrollInfo）。
		public static bool IsVerticalOffsetWithinDocumentArea(this TextEditor textEditor, double offset)
		{
			TextView textView = textEditor.TextArea.TextView;
			// 阶段 5：用 TextView.Bounds.Height 作为视口高度，VerticalOffset 为当前滚动偏移。
			// 文档总高度 = textView.Bounds.Height + textView.VerticalOffset（近似）。
			double viewportHeight = textView.Bounds.Height;
			double extentHeight = textView.Bounds.Height + textView.VerticalOffset;
			if (offset + viewportHeight > extentHeight)
			{
				return false;
			}
			return true;
		}

		public static bool IsHorizontalOffsetWithinDocumentArea(this TextEditor textEditor, double offset)
		{
			TextView textView = textEditor.TextArea.TextView;
			// 阶段 5：同上，用 Bounds.Width + HorizontalOffset 近似文档总宽度。
			double viewportWidth = textView.Bounds.Width;
			double extentWidth = textView.Bounds.Width + textView.HorizontalOffset;
			if (offset + viewportWidth > extentWidth)
			{
				return false;
			}
			return true;
		}
	}
}
