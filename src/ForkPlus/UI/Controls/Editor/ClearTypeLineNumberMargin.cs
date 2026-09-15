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

		// 修复（2026-09-15，"行号与代码细微垂直偏移"）：行号此前画在行槽顶（VisualTop -
		// ScrollOffset.Y），而代码文本按 TextLine 实际高度在槽内垂直居中（AvaloniaEdit
		// VisualLineDrawingVisual 的 num3 偏移），且含 CJK 的行（全局回退 Noto Sans CJK SC
		// 行框更高）偏移更大——诊断探针实测 ASCII 行文本顶=槽顶+1.22px、CJK 行=槽顶+5.42px，
		// 行号与代码逐行错位且不等距。改为返回该行代码文本的基线 Y（视口坐标），调用方把
		// 行号/标记按自身 FormattedText.Baseline 落到同一基线上——与行号字号、行内容字体
		//（CJK/ASCII 混排）均无关，恒与代码基线对齐。
		protected double GetLineTextBaselineY(global::AvaloniaEdit.Rendering.VisualLine visualLine)
		{
			global::AvaloniaEdit.Rendering.TextView textView = base.TextView;
			if (textView == null || visualLine.TextLines.Count == 0)
			{
				return visualLine.VisualTop - ((textView != null) ? textView.ScrollOffset.Y : 0.0);
			}
			return visualLine.GetTextLineVisualYPosition(visualLine.TextLines[0], global::AvaloniaEdit.Rendering.VisualYPosition.Baseline) - textView.ScrollOffset.Y;
		}
	}
}
