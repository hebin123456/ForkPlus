using ForkPlus.UI.Helpers;
using Avalonia.Controls.Primitives;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;

namespace ForkPlus.UI.Helpers
{
	internal static class TextEditorExtensions
	{
		// Migration note：WPF 经 IScrollInfo 读滚动区；AvaloniaEdit TextView 实现 IScrollable
		//（Extent/Viewport 为 Size），去掉 WPF IScrollInfo 转型直接读属性。
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
