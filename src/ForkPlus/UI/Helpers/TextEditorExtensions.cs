using ForkPlus.UI.Helpers;
using System.Windows.Controls.Primitives;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;

namespace ForkPlus.UI.Helpers
{
	internal static class TextEditorExtensions
	{
		public static bool IsVerticalOffsetWithinDocumentArea(this TextEditor textEditor, double offset)
		{
			TextView textView = textEditor.TextArea.TextView;
			double extentHeight = ((IScrollInfo)textView).ExtentHeight;
			double viewportHeight = ((IScrollInfo)textView).ViewportHeight;
			if (offset + viewportHeight > extentHeight)
			{
				return false;
			}
			return true;
		}

		public static bool IsHorizontalOffsetWithinDocumentArea(this TextEditor textEditor, double offset)
		{
			TextView textView = textEditor.TextArea.TextView;
			double extentWidth = ((IScrollInfo)textView).ExtentWidth;
			double viewportWidth = ((IScrollInfo)textView).ViewportWidth;
			if (offset + viewportWidth > extentWidth)
			{
				return false;
			}
			return true;
		}
	}
}
