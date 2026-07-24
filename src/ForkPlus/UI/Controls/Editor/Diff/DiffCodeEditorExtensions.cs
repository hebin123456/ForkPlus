using System.Collections.ObjectModel;
using ForkPlus.Git.Diff.Presentation;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public static class DiffCodeEditorExtensions
	{
		public static void ScrollToPreviousCustomHunk(this DiffCodeEditor editor)
		{
			VisualDiff visualDiff = editor.VisualPatch?.VisualDiff;
			if (visualDiff == null)
			{
				return;
			}
			TextView textView = editor.TextArea.TextView;
			DocumentLine documentLineByVisualTop = textView.GetDocumentLineByVisualTop(textView.ScrollOffset.Y + 1.0);
			if (documentLineByVisualTop == null)
			{
				return;
			}
			int startOffset = documentLineByVisualTop.Offset;
			int? chunkIndex = GetChunkIndex(visualDiff, startOffset);
			if (chunkIndex.HasValue)
			{
				int valueOrDefault = chunkIndex.GetValueOrDefault();
				int? num = GetPreviousCustomHunkLineNumber(visualDiff.VisualChunks, valueOrDefault, startOffset) ?? GetPreviousCustomHunkLineNumber(visualDiff.VisualChunks, valueOrDefault - 1, startOffset);
				if (num.HasValue)
				{
					int valueOrDefault2 = num.GetValueOrDefault();
					editor.ScrollTo(valueOrDefault2, 0, VisualYPosition.LineBottom, 0.0, 0.0);
				}
			}
		}

		public static void ScrollToNextCustomHunk(this DiffCodeEditor editor)
		{
			VisualDiff visualDiff = editor.VisualPatch?.VisualDiff;
			if (visualDiff == null)
			{
				return;
			}
			TextView textView = editor.TextArea.TextView;
			double nextLineOffset = (textView.DefaultLineHeight > 1.0) ? textView.DefaultLineHeight : 1.0;
			DocumentLine documentLineByVisualTop = textView.GetDocumentLineByVisualTop(textView.ScrollOffset.Y + nextLineOffset + 1.0);
			if (documentLineByVisualTop == null)
			{
				return;
			}
			int startOffset = documentLineByVisualTop.Offset;
			int? chunkIndex = GetChunkIndex(visualDiff, startOffset);
			if (chunkIndex.HasValue)
			{
				int valueOrDefault = chunkIndex.GetValueOrDefault();
				int? num = GetNextCustomHunkLineNumber(visualDiff.VisualChunks, valueOrDefault, startOffset) ?? GetNextCustomHunkLineNumber(visualDiff.VisualChunks, valueOrDefault + 1, startOffset);
				if (num.HasValue)
				{
					int valueOrDefault2 = num.GetValueOrDefault();
					editor.ScrollTo(valueOrDefault2, 0, VisualYPosition.LineBottom, 0.0, 0.0);
				}
			}
		}

		private static int? GetChunkIndex(VisualDiff visualDiff, int characterIndex)
		{
			for (int i = 0; i < visualDiff.VisualChunks.Length; i++)
			{
				if (visualDiff.VisualChunks[i].CharRange.Contains(characterIndex))
				{
					return i;
				}
			}
			return null;
		}

		[Null]
		private static int? GetPreviousCustomHunkLineNumber(VisualChunk[] visualChunks, int chunkIndex, int characterIndex)
		{
			if (chunkIndex < 0)
			{
				return null;
			}
			VisualChunk visualChunk = visualChunks[chunkIndex];
			Range[] customHunks = visualChunk.CustomHunks;
			for (int num = customHunks.Length - 1; num >= 0; num--)
			{
				Range range = customHunks[num];
				if (visualChunk.VisualLines[range.Start].Range.Start < characterIndex)
				{
					int start = visualChunk.CustomHunks[num].Start;
					return visualChunk.VisualLines[start].LineNumber;
				}
			}
			return null;
		}

		[Null]
		private static int? GetNextCustomHunkLineNumber(VisualChunk[] chunks, int chunkIndex, int characterIndex)
		{
			if (chunkIndex >= chunks.Length)
			{
				return null;
			}
			VisualChunk visualChunk = chunks[chunkIndex];
			Range[] customHunks = visualChunk.CustomHunks;
			for (int i = 0; i < customHunks.Length; i++)
			{
				Range range = customHunks[i];
				if (visualChunk.VisualLines[range.Start].Range.Start > characterIndex && (chunkIndex != 0 || i != 0))
				{
					int start = visualChunk.CustomHunks[i].Start;
					return visualChunk.VisualLines[start].LineNumber;
				}
			}
			return null;
		}
	}
}
