using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using Theme = ForkPlus.UI.Theme;

namespace ForkPlus.UI.Controls.Editor
{
	public class ClearTypeLineNumberMargin : LineNumberMargin
	{
		// 阶段 5：WPF AvalonEdit LineNumberMargin 有 protected Typeface typeface / double emSize
		// 字段，供派生类（CodeEditorLineNumberMargin / DiffLineNumberMargin）设置字体度量。
		// Avalonia.AvaloniaEdit 的 LineNumberMargin 无这两个字段，此处补齐以兼容派生类代码。
		protected Typeface typeface;
		protected double emSize;

		// 阶段 5: Avalonia 11.3 中 Control 的渲染方法为 public override void Render(DrawingContext)
		public override void Render(DrawingContext drawingContext)
		{
			drawingContext.DrawRectangle(ForkPlus.UI.Theme.CodeEditor.BackgroundBrush, null, new Rect(0.0, 0.0, Bounds.Size.Width, Bounds.Size.Height));
		}
	}
}
