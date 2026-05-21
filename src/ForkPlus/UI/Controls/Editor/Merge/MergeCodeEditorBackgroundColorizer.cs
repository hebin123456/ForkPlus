using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ForkPlus.Git.Merge;
using ForkPlus.Git.Merge.Presentation;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace ForkPlus.UI.Controls.Editor.Merge
{
	public class MergeCodeEditorBackgroundColorizer : IBackgroundRenderer
	{
		private MergeCodeEditor _editor;

		public KnownLayer Layer => KnownLayer.Background;

		public MergeCodeEditorBackgroundColorizer(MergeCodeEditor editor)
		{
			_editor = editor;
		}

		public void Draw(TextView textView, DrawingContext drawingContext)
		{
			MergeConflictView mergeConflictView = _editor.MergeConflictView;
			if (mergeConflictView == null || !textView.VisualLinesValid)
			{
				return;
			}
			ReadOnlyCollection<VisualLine> visualLines = textView.VisualLines;
			if (visualLines.Count <= 0)
			{
				return;
			}
			int offset = visualLines.First().FirstDocumentLine.Offset;
			int endOffset = visualLines.Last().LastDocumentLine.EndOffset;
			Range other = new Range(offset, endOffset);
			MergeConflictView.Chunk[] chunks = mergeConflictView.Chunks;
			foreach (MergeConflictView.Chunk chunk in chunks)
			{
				if (!chunk.Range.Overlaps(other))
				{
					continue;
				}
				MergeConflictView.Line[] lines;
				if (chunk.Node is MergeConflict.ConflictChunk)
				{
					lines = chunk.Lines;
					foreach (MergeConflictView.Line line in lines)
					{
						if (line.Range.Overlaps(other) && line.Node is MergeConflict.SelectableLine selectableLine)
						{
							if (selectableLine.ViewMode == MergeConflictPart.Remote)
							{
								DrawRectangle(drawingContext, textView, line, HighlightingType.MergeRemote.GetHighlightBrush(_editor.Theme));
							}
							else if (selectableLine.ViewMode == MergeConflictPart.Local)
							{
								DrawRectangle(drawingContext, textView, line, HighlightingType.MergeLocal.GetHighlightBrush(_editor.Theme));
							}
						}
					}
				}
				else
				{
					lines = chunk.Lines;
					foreach (MergeConflictView.Line line2 in lines)
					{
						if (!line2.Range.Overlaps(other))
						{
							continue;
						}
						MergeConflict.Line node = line2.Node;
						if (node != null)
						{
							if (node.ChangeType == ContextType.Add)
							{
								DrawRectangle(drawingContext, textView, line2, HighlightingType.MergeAdd.GetHighlightBrush(_editor.Theme));
							}
							else if (node.ChangeType == ContextType.Remove)
							{
								DrawRectangle(drawingContext, textView, line2, HighlightingType.MergeRemove.GetHighlightBrush(_editor.Theme));
							}
						}
					}
				}
				lines = chunk.AlignmentLines;
				foreach (MergeConflictView.Line line3 in lines)
				{
					if (line3.Range.Overlaps(other))
					{
						if (_editor.ViewMode == MergeConflictPart.Merged && chunk.Node is MergeConflict.ConflictChunk && chunk.Lines.Length == 0)
						{
							DrawRectangle(drawingContext, textView, line3, HighlightingType.MergeUnresolved.GetHighlightBrush(_editor.Theme));
						}
						else
						{
							DrawRectangle(drawingContext, textView, line3, HighlightingType.Alignment.GetHighlightBrush(_editor.Theme));
						}
					}
				}
			}
		}

		private static void DrawRectangle(DrawingContext context, TextView textView, ISegment range, Brush brush)
		{
			foreach (Rect item in BackgroundGeometryBuilder.GetRectsForSegment(textView, range, extendToFullWidthAtLineEnd: true))
			{
				Rect rectangle = new Rect(item.X, item.Y, textView.ActualWidth + textView.HorizontalOffset, item.Height);
				context.DrawRectangle(brush, null, rectangle);
			}
		}
	}
}
