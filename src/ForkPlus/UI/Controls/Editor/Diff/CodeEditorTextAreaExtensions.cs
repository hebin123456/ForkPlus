using System;
using ForkPlus.Git.Diff.Presentation;
using AvaloniaEdit.Document;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public static class CodeEditorTextAreaExtensions
	{
		public static (int, double) GetFirstVisibleCharacterPosition(this CodeEditor editor)
		{
			double scrollPosition = editor.GetScrollPosition();
			DocumentLine documentLineByVisualTop = editor.TextArea.TextView.GetDocumentLineByVisualTop(scrollPosition + 1.0);
			double item = scrollPosition - editor.TextArea.TextView.GetVisualTopByDocumentLine(documentLineByVisualTop.LineNumber);
			return (documentLineByVisualTop.Offset, item);
		}

		public static double? GetScrollPositionByCharacterIndex(this CodeEditor editor, int characterIndex)
		{
			if (characterIndex < editor.Document.TextLength)
			{
				DocumentLine lineByOffset = editor.Document.GetLineByOffset(characterIndex);
				return editor.TextArea.TextView.GetVisualTopByDocumentLine(lineByOffset.LineNumber);
			}
			return null;
		}

		public static CodeEditorScrollPositionCache.Position? GetOriginalLinePosition(this VisualDiff visualDiff, int characterIndex, double offsetY)
		{
			VisualChunk[] visualChunks = visualDiff.VisualChunks;
			foreach (VisualChunk visualChunk in visualChunks)
			{
				if (!visualChunk.CharRange.Contains(characterIndex))
				{
					continue;
				}
				int num = visualChunk.Node.FromStart;
				int num2 = visualChunk.Node.ToStart;
				Range? headerCharRange = visualChunk.HeaderCharRange;
				if (headerCharRange.HasValue && headerCharRange.GetValueOrDefault().Contains(characterIndex))
				{
					return new CodeEditorScrollPositionCache.Position(Math.Max(0, num - 1), Math.Max(0, num2 - 1), offsetY);
				}
				VisualSubChunk[] visualSubChunks = visualChunk.VisualSubChunks;
				foreach (VisualSubChunk visualSubChunk in visualSubChunks)
				{
					for (int k = visualSubChunk.PreContextLines.Start; k < visualSubChunk.PreContextLines.End; k++)
					{
						if (visualChunk.VisualLines[k].Range.Contains(characterIndex))
						{
							return new CodeEditorScrollPositionCache.Position(num, num2, offsetY);
						}
						num++;
						num2++;
					}
					for (int l = visualSubChunk.DeletedLines.Start; l < visualSubChunk.DeletedLines.End; l++)
					{
						if (visualChunk.VisualLines[l].Range.Contains(characterIndex))
						{
							return new CodeEditorScrollPositionCache.Position(num, null, offsetY);
						}
						num++;
					}
					for (int m = visualSubChunk.AddedLines.Start; m < visualSubChunk.AddedLines.End; m++)
					{
						if (visualChunk.VisualLines[m].Range.Contains(characterIndex))
						{
							return new CodeEditorScrollPositionCache.Position(null, num2, offsetY);
						}
						num2++;
					}
					for (int n = visualSubChunk.PostContextLines.Start; n < visualSubChunk.PostContextLines.End; n++)
					{
						if (visualChunk.VisualLines[n].Range.Contains(characterIndex))
						{
							return new CodeEditorScrollPositionCache.Position(num, num2, offsetY);
						}
						num++;
						num2++;
					}
				}
			}
			return null;
		}

		public static int? GetVisualCharacterIndex(this VisualDiff visualDiff, CodeEditorScrollPositionCache.Position originalLineNumber)
		{
			if (!originalLineNumber.Src.HasValue && !originalLineNumber.Dst.HasValue)
			{
				return null;
			}
			VisualChunk[] visualChunks = visualDiff.VisualChunks;
			foreach (VisualChunk visualChunk in visualChunks)
			{
				if (originalLineNumber.Src == visualChunk.Node.FromStart - 1 || originalLineNumber.Dst == visualChunk.Node.ToStart - 1)
				{
					return visualChunk.CharRange.Start;
				}
				int num = visualChunk.Node.FromStart;
				int num2 = visualChunk.Node.ToStart;
				if (num >= originalLineNumber.Src.GetValueOrDefault(int.MaxValue) || num2 >= originalLineNumber.Dst.GetValueOrDefault(int.MaxValue))
				{
					return visualChunk.CharRange.Start;
				}
				VisualSubChunk[] visualSubChunks = visualChunk.VisualSubChunks;
				foreach (VisualSubChunk visualSubChunk in visualSubChunks)
				{
					for (int k = visualSubChunk.PreContextLines.Start; k < visualSubChunk.PreContextLines.End; k++)
					{
						if (num >= originalLineNumber.Src.GetValueOrDefault(int.MaxValue) || num2 >= originalLineNumber.Dst.GetValueOrDefault(int.MaxValue))
						{
							return visualChunk.VisualLines[k].Range.Start;
						}
						num++;
						num2++;
					}
					for (int l = visualSubChunk.DeletedLines.Start; l < visualSubChunk.DeletedLines.End; l++)
					{
						if (num >= originalLineNumber.Src.GetValueOrDefault(int.MaxValue))
						{
							return visualChunk.VisualLines[l].Range.Start;
						}
						num++;
					}
					for (int m = visualSubChunk.AddedLines.Start; m < visualSubChunk.AddedLines.End; m++)
					{
						if (num2 >= originalLineNumber.Dst.GetValueOrDefault(int.MaxValue))
						{
							return visualChunk.VisualLines[m].Range.Start;
						}
						num2++;
					}
					for (int n = visualSubChunk.PostContextLines.Start; n < visualSubChunk.PostContextLines.End; n++)
					{
						if (num >= originalLineNumber.Src.GetValueOrDefault(int.MaxValue) || num2 >= originalLineNumber.Dst.GetValueOrDefault(int.MaxValue))
						{
							return visualChunk.VisualLines[n].Range.Start;
						}
						num++;
						num2++;
					}
				}
			}
			return null;
		}
	}
}
