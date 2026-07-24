using Avalonia.Controls.Primitives;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;

namespace ForkPlus.UI.Helpers
{
	internal static class TextEditorExtensions
	{
		// 阶段 4 里程碑 4.7-a：System.Windows.Controls.Primitives.IScrollInfo → Avalonia.Controls.Primitives.IScrollInfo。
		// Avalonia 的 IScrollInfo 用 Size Extent / Size Viewport（非 WPF 的 ExtentHeight/ViewportHeight 双精度属性）。
		// 用 as 安全转型：若 Avalonia.AvalonEdit TextView 未实现 IScrollInfo，回退为 true（不阻断滚动逻辑），待 build 验证。
		public static bool IsVerticalOffsetWithinDocumentArea(this TextEditor textEditor, double offset)
		{
			TextView textView = textEditor.TextArea.TextView;
			IScrollInfo scrollInfo = textView as IScrollInfo;
			// TODO(4.7-a): 验证 Avalonia.AvalonEdit TextView 是否实现 Avalonia IScrollInfo；若否需改用 TextView 自身滚动范围属性。
			if (scrollInfo == null)
			{
				return true;
			}
			double extentHeight = scrollInfo.Extent.Height;
			double viewportHeight = scrollInfo.Viewport.Height;
			if (offset + viewportHeight > extentHeight)
			{
				return false;
			}
			return true;
		}

		public static bool IsHorizontalOffsetWithinDocumentArea(this TextEditor textEditor, double offset)
		{
			TextView textView = textEditor.TextArea.TextView;
			IScrollInfo scrollInfo = textView as IScrollInfo;
			// TODO(4.7-a): 同上，验证 TextView 的 IScrollInfo 实现。
			if (scrollInfo == null)
			{
				return true;
			}
			double extentWidth = scrollInfo.Extent.Width;
			double viewportWidth = scrollInfo.Viewport.Width;
			if (offset + viewportWidth > extentWidth)
			{
				return false;
			}
			return true;
		}
	}
}
