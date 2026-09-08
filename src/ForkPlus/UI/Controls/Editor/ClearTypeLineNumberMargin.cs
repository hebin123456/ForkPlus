using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls.Editor
{
	public class ClearTypeLineNumberMargin : LineNumberMargin
	{
		public override void Render(DrawingContext drawingContext)
		{
			drawingContext.DrawRectangle(global::ForkPlus.UI.Theme.CodeEditor.BackgroundBrush, null, new Rect(0.0, 0.0, base.Bounds.Size.Width, base.Bounds.Size.Height));
		}
	}
}
