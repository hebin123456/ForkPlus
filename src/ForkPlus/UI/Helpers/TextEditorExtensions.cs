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

		/// <summary>把期望垂直偏移夹到文档区内（0 .. extent-Viewport）。</summary>
		/// <remarks>与 IsVerticalOffsetWithinDocumentArea 互补：后者只判断"是否在区内"，
		/// 前者给出"区内钳制值"。FileDiff 左右同步用：偏长侧滚到偏短侧够不到的位置时，
		/// 之前直接跳过（偏短侧被丢在原地、滚动条还有空余可继续下滚 → 两边"不对齐"）；
		/// 修复为夹到偏短侧自己的文档末尾，两侧在各自文档末端"对齐定格"。</remarks>
		public static double ClampVerticalOffsetToDocumentArea(this TextEditor textEditor, double desired)
		{
			TextView textView = textEditor.TextArea.TextView;
			double extentHeight = ((IScrollable)textView).Extent.Height;
			double viewportHeight = ((IScrollable)textView).Viewport.Height;
			double max = extentHeight - viewportHeight;
			if (max < 0.0)
			{
				max = 0.0;
			}
			return desired < max ? desired : max;
		}

		/// <summary>把期望水平偏移夹到文档区内（0 .. extent-Viewport）。</summary>
		public static double ClampHorizontalOffsetToDocumentArea(this TextEditor textEditor, double desired)
		{
			TextView textView = textEditor.TextArea.TextView;
			double extentWidth = ((IScrollable)textView).Extent.Width;
			double viewportWidth = ((IScrollable)textView).Viewport.Width;
			double max = extentWidth - viewportWidth;
			if (max < 0.0)
			{
				max = 0.0;
			}
			return desired < max ? desired : max;
		}
	}
}
