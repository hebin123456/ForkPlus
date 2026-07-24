using System;
using ForkPlus.Biturbo;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.Settings;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	internal class SyntaxHighlighting : DocumentColorizingTransformer
	{
		private static readonly Action<VisualLineElement>[] _syntaxHighlightings;

		[Null]
		private Highlighting[] _highlightings;

		static SyntaxHighlighting()
		{
			_syntaxHighlightings = new Action<VisualLineElement>[9]
			{
				delegate(VisualLineElement e)
				{
					HighlightElement(e, HighlightingType.SyntaxComment);
				},
				delegate(VisualLineElement e)
				{
					HighlightElement(e, HighlightingType.SyntaxString);
				},
				delegate(VisualLineElement e)
				{
					HighlightElement(e, HighlightingType.SyntaxKeyword);
				},
				delegate(VisualLineElement e)
				{
					HighlightElement(e, HighlightingType.SyntaxType);
				},
				delegate(VisualLineElement e)
				{
					HighlightElement(e, HighlightingType.SyntaxCommand);
				},
				delegate(VisualLineElement e)
				{
					HighlightElement(e, HighlightingType.SyntaxAttribute);
				},
				delegate(VisualLineElement e)
				{
					HighlightElement(e, HighlightingType.SyntaxVariable);
				},
				delegate(VisualLineElement e)
				{
					HighlightElement(e, HighlightingType.SyntaxValue);
				},
				delegate(VisualLineElement e)
				{
					HighlightElement(e, HighlightingType.SyntaxNumber);
				}
			};
		}

		public void Highlight(string filepath, string input, [Null] Range[] ranges = null)
		{
			ranges = ranges ?? new Range[1]
			{
				new Range(0, input.Length)
			};
			_highlightings = HighlightImpl(filepath, input, ranges);
		}

		public void Highlight([Null] VisualPatch visualPatch)
		{
			if (visualPatch != null)
			{
				string text = visualPatch.VisualDiff.Node.OldFilepath ?? visualPatch.VisualDiff.Node.NewFilepath;
				if (text != null && !visualPatch.VisualDiff.Node.IsMinified)
				{
					Range[] ranges = visualPatch.VisualDiff.VisualChunks.Map((VisualChunk x) => x.InnerRange);
					_highlightings = HighlightImpl(text, visualPatch.StringValue, ranges);
					return;
				}
			}
			Clear();
		}

		public void Clear()
		{
			_highlightings = null;
		}

		protected override void ColorizeLine(DocumentLine line)
		{
			if (line.IsDeleted)
			{
				return;
			}
			Highlighting[] highlightings = _highlightings;
			if (highlightings == null)
			{
				return;
			}
			_ = ForkPlusSettings.Default.Theme;
			Range lineRange = new Range(line.Offset, line.EndOffset);
			int num = highlightings.BinarySearchBy((Highlighting x) => x.Range.Start.CompareTo(lineRange.Start));
			if (num < 0)
			{
				num = ~num;
			}
			for (int i = 0; i < highlightings.Length; i++)
			{
				Highlighting highlighting = highlightings[i];
				if (highlighting.Range.Start >= lineRange.Start)
				{
					if (!highlighting.Range.Overlaps(lineRange))
					{
						break;
					}
					int endOffset = Math.Min(highlighting.Range.End, lineRange.End);
					ChangeLinePart(highlighting.Range.Start, endOffset, _syntaxHighlightings[highlighting.Style]);
				}
			}
		}

		private static Highlighting[] HighlightImpl(string filepath, string input, Range[] ranges)
		{
			using (new BenchmarkAssert(100, "syntax highlighting '" + filepath + "'"))
			{
				BtRange[] array = ranges.Map(delegate(Range x)
				{
					BtRange result = default(BtRange);
					result.start = (uint)x.Start;
					result.end = (uint)x.End;
					return result;
				});
				BtHighlightedDiff out_result = default(BtHighlightedDiff);
				BtResult btResult = Bt.bt_highlight_syntax(filepath, input, array, array.Length, ref out_result);
				switch (btResult)
				{
				case BtResult.ErrNotFound:
					return null;
				default:
					Log.Error("Syntax highlighting failed:\n" + btResult.ToGitCommandError().FriendlyDescription);
					return null;
				case BtResult.Ok:
				{
					Highlighting[] structArray = out_result.items.GetStructArray(out_result.items_len, (BtHighlighedRange btHighlightedItem) => new Highlighting(new Range((int)btHighlightedItem.range_utf16.start, (int)btHighlightedItem.range_utf16.end), btHighlightedItem.style));
					Bt.bt_release_highlight_syntax(ref out_result);
					return structArray;
				}
				}
			}
		}

		private static void HighlightElement(VisualLineElement e, HighlightingType highlightingType)
		{
			e.TextRunProperties.SetForegroundBrush(highlightingType.GetHighlightBrush(ForkPlusSettings.Default.Theme));
		}
	}
}
