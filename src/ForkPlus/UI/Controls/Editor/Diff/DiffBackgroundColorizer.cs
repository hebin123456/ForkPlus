using Avalonia;
using Avalonia.Media;
using ForkPlus.Settings;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public class DiffBackgroundColorizer : IBackgroundRenderer
	{
		private readonly TextSegment _fullWidthSegment;

		private Rect _rectangle;

		public HighlightingSource[] HighlightingSource { get; set; }

		public KnownLayer Layer => KnownLayer.Background;

		public DiffBackgroundColorizer()
		{
			_fullWidthSegment = new TextSegment();
			_rectangle = default(Rect);
		}

		public void Draw(TextView textView, DrawingContext drawingContext)
		{
			if (HighlightingSource == null || !textView.VisualLinesValid)
			{
				return;
			}
			ThemeType theme = ForkPlusSettings.Default.Theme;
			HighlightingSource[] highlightingSource = HighlightingSource;
			foreach (HighlightingSource highlightingSource2 in highlightingSource)
			{
				IBrush highlightBrush = highlightingSource2.HighlightingType.GetHighlightBrush(theme);
				if (highlightingSource2.HighlightingType == HighlightingType.ExactAdd || highlightingSource2.HighlightingType == HighlightingType.ExactRemove)
				{
					BackgroundGeometryBuilder backgroundGeometryBuilder = new BackgroundGeometryBuilder
					{
						AlignToWholePixels = true
					};
					backgroundGeometryBuilder.AddSegment(textView, highlightingSource2.Segment);
					drawingContext.DrawGeometry(highlightBrush, null, backgroundGeometryBuilder.CreateGeometry());
					continue;
				}
				DocumentLine lineByOffset = textView.Document.GetLineByOffset(highlightingSource2.Segment.StartOffset);
				_fullWidthSegment.StartOffset = highlightingSource2.Segment.StartOffset;
				_fullWidthSegment.EndOffset = highlightingSource2.Segment.EndOffset;
				if (_fullWidthSegment.StartOffset != lineByOffset.Offset)
				{
					_fullWidthSegment.StartOffset = lineByOffset.Offset;
				}
				foreach (Rect item in BackgroundGeometryBuilder.GetRectsForSegment(textView, _fullWidthSegment, extendToFullWidthAtLineEnd: true))
				{
					// 阶段 5：Avalonia Rect.X/Y/Width/Height 是只读属性（结构体不可变），
					// 需整体重新赋值，不能逐字段设置。
					_rectangle = new Rect(0.0, item.Top, textView.Bounds.Width + textView.HorizontalOffset, item.Height);
					drawingContext.DrawRectangle(highlightBrush, null, _rectangle);
				}
			}
		}
	}
}
