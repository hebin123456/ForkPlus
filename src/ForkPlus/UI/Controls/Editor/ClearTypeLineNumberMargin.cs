using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using Theme = ForkPlus.UI.Theme;

namespace ForkPlus.UI.Controls.Editor
{
	public class ClearTypeLineNumberMargin : LineNumberMargin
	{
		// 阶段 5: Avalonia 11.3 中 Control 的渲染方法为 public override void Render(DrawingContext)
		public override void Render(DrawingContext drawingContext)
		{
			drawingContext.DrawRectangle(ForkPlus.UI.Theme.CodeEditor.BackgroundBrush, null, new Rect(0.0, 0.0, Bounds.Size.Width, Bounds.Size.Height));
		}
	}
}
