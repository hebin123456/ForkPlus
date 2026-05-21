using System.Windows;
using System.Windows.Media;
using ForkPlus.Settings;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

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
				Brush highlightBrush = highlightingSource2.HighlightingType.GetHighlightBrush(theme);
				highlightBrush.Freeze();
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
					_rectangle.X = 0.0;
					_rectangle.Y = item.Top;
					_rectangle.Width = textView.ActualWidth + textView.HorizontalOffset;
					_rectangle.Height = item.Height;
					drawingContext.DrawRectangle(highlightBrush, null, _rectangle);
				}
			}
		}
	}
}
