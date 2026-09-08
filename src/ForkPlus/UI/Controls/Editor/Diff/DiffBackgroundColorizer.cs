using Avalonia;
using Avalonia.Media;
using ForkPlus.Settings;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

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
				// Migration note：WPF Rect.X/Y/Width/Height 可变属性 → Avalonia Rect 是不可变结构体，需整体重建。
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
					// Migration note：WPF Rect 属性赋值 → Avalonia Rect 不可变，new Rect(x, y, w, h)。
				_rectangle = new Rect(0.0, item.Top, textView.Bounds.Width + textView.ScrollOffset.X, item.Height);
				drawingContext.DrawRectangle(highlightBrush, null, _rectangle);
				}
			}
		}
	}
}
