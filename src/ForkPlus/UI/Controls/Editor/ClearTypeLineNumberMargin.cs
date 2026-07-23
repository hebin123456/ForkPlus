using Avalonia;
using Avalonia.Media;
using ICSharpCode.AvalonEdit.Editing;

namespace ForkPlus.UI.Controls.Editor
{
	public class ClearTypeLineNumberMargin : LineNumberMargin
	{
		protected override void OnRender(DrawingContext drawingContext)
		{
			drawingContext.DrawRectangle(Theme.CodeEditor.BackgroundBrush, null, new Rect(0.0, 0.0, base.RenderSize.Width, base.RenderSize.Height));
		}
	}
}
