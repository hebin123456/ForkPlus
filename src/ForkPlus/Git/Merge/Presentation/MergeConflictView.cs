using System;
using System.Collections.Generic;
using ForkPlus.Git.Diff;
using AvaloniaEdit.Document;

namespace ForkPlus.Git.Merge.Presentation
{
	public class MergeConflictView
	{
		public class Chunk
		{
			public Range Range { get; }

			public Range LineRange { get; }

			public Line[] Lines { get; }

			public Line[] AlignmentLines { get; }

			public MergeConflict.Chunk Node { get; }

			public Chunk(Range range, Range lineRange, Line[] lines, Line[] alignmentLines, MergeConflict.Chunk node)
			{
				Range = range;
				LineRange = lineRange;
				Lines = lines;
				AlignmentLines = alignmentLines;
				Node = node;
			}

			public void SelectAllLines()
			{
				Line[] lines = Lines;
				for (int i = 0; i < lines.Length; i++)
				{
					if (lines[i].Node is MergeConflict.SelectableLine selectableLine)
					{
						selectableLine.Deselect();
						selectableLine.Select();
					}
				}
			}

			public void DeselectAllLines()
			{
				Line[] lines = Lines;
				for (int i = 0; i < lines.Length; i++)
				{
					if (lines[i].Node is MergeConflict.SelectableLine selectableLine)
					{
						selectableLine.Deselect();
					}
				}
			}
		}

		public class Line : ISegment
		{
			public Range Range { get; }

			public int LineNumber { get; }

			public MergeConflict.Line Node { get; }

			int ISegment.Offset => Range.Start;

			int ISegment.Length => Range.Length - 1;

			int ISegment.EndOffset => Range.End - 1;

			public Line(Range range, int lineNumber, MergeConflict.Line node)
			{
				Range = range;
				LineNumber = lineNumber;
				Node = node;
			}

			public void MeldWith(Line other)
			{
				MergeConflict.Line node = Node;
				if (node != null)
				{
					MergeConflict.Line node2 = other.Node;
					if (node2 != null)
					{
						node.MeldWith(node2);
						return;
					}
				}
				Log.Error("cannot meld service lines");
			}

			public void RemoveSubrange(Range rangeToRemove)
			{
				int start = rangeToRemove.Start - Range.Start;
				int end = Math.Min(Range.End, rangeToRemove.End - Range.Start);
				Node.RemoveSubrange(new Range(start, end));
			}

			public MergeConflict.Line Insert(string line, int location)
			{
				return Node.Insert(line, location - Range.Start);
			}
		}

		public string StringValue { get; }

		public Chunk[] Chunks { get; }

		public MergeConflict Node { get; }

		public MergeConflictPart ViewMode { get; }

		public static MergeConflictView Create(MergeConflict mergeConflict, MergeConflictPart viewMode, bool formatting)
		{
			PresentationContext presentationContext = new PresentationContext();
			List<Chunk> list = new List<Chunk>(mergeConflict.Chunks.Length);
			MergeConflict.Chunk[] chunks = mergeConflict.Chunks;
			foreach (MergeConflict.Chunk chunk in chunks)
			{
				Chunk item = CreateView(presentationContext, chunk, viewMode, formatting);
				list.Add(item);
			}
			string text = presentationContext.ResultString();
			if (text.Length > 0 && text[text.Length - 1] == '\n' && mergeConflict.NoNewLineAtEndOfFile)
			{
				text = text.Substring(0, text.Length - 1);
			}
			return new MergeConflictView(text, list.ToArray(), mergeConflict, viewMode);
		}

		public MergeConflictView(string stringValue, Chunk[] chunks, MergeConflict node, MergeConflictPart viewMode)
		{
			StringValue = stringValue;
			Chunks = chunks;
			Node = node;
			ViewMode = viewMode;
		}

		private static Chunk CreateView(PresentationContext ctx, MergeConflict.Chunk chunk, MergeConflictPart viewMode, bool formatting)
		{
			int lineNumber = ctx.LineNumber;
			int cursor = ctx.Cursor;
			List<Line> list = new List<Line>();
			if (viewMode == MergeConflictPart.Merged)
			{
				list.Capacity = chunk.ResultLines.Count;
				foreach (MergeConflict.Line resultLine in chunk.ResultLines)
				{
					list.Add(CreateView(ctx, viewMode, resultLine));
				}
			}
			else if (chunk is MergeConflict.ChangeChunk changeChunk)
			{
				switch (viewMode)
				{
				case MergeConflictPart.Remote:
				{
					list.Capacity = changeChunk.RemoteLines.Length;
					MergeConflict.SelectableLine[] localLines = changeChunk.RemoteLines;
					foreach (MergeConflict.SelectableLine selectableLine2 in localLines)
					{
						if (!(chunk is MergeConflict.ConflictChunk) || selectableLine2.ChangeType != 0)
						{
							list.Add(CreateView(ctx, viewMode, selectableLine2));
						}
					}
					break;
				}
				case MergeConflictPart.Local:
				{
					list.Capacity = changeChunk.LocalLines.Length;
					MergeConflict.SelectableLine[] localLines = changeChunk.LocalLines;
					foreach (MergeConflict.SelectableLine selectableLine in localLines)
					{
						if (!(chunk is MergeConflict.ConflictChunk) || selectableLine.ChangeType != 0)
						{
							list.Add(CreateView(ctx, viewMode, selectableLine));
						}
					}
					break;
				}
				}
			}
			else if (chunk is MergeConflict.ContextChunk contextChunk && (viewMode == MergeConflictPart.Remote || viewMode == MergeConflictPart.Local))
			{
				list.Capacity = contextChunk.Lines.Length;
				MergeConflict.Line[] lines = contextChunk.Lines;
				foreach (MergeConflict.Line line in lines)
				{
					list.Add(CreateView(ctx, viewMode, line));
				}
			}
			List<Line> list2 = new List<Line>();
			if (chunk.Height > list.Count && formatting)
			{
				for (int j = 0; j < chunk.Height - list.Count; j++)
				{
					if (chunk is MergeConflict.ConflictChunk conflictChunk && viewMode == MergeConflictPart.Merged && j == 0 && !conflictChunk.IsResolved)
					{
						list2.Add(CreateAlignmentLine(ctx, "--- Merge Conflict ---\n"));
					}
					else
					{
						list2.Add(CreateAlignmentLine(ctx));
					}
				}
			}
			return new Chunk(new Range(cursor, ctx.Cursor), new Range(lineNumber, ctx.LineNumber), list.ToArray(), list2.ToArray(), chunk);
		}

		public bool RangeContainsAlignmentLines(Range range)
		{
			new List<Line>();
			Range other = new Range(range.Start, range.End + 1);
			for (int i = 0; i < Chunks.Length; i++)
			{
				Chunk chunk = Chunks[i];
				if (chunk.Range.Start > other.End)
				{
					break;
				}
				if (!chunk.Range.Overlaps(other))
				{
					continue;
				}
				for (int j = 0; j < chunk.AlignmentLines.Length; j++)
				{
					Line line = chunk.AlignmentLines[j];
					if (line.Range.Start > other.End)
					{
						break;
					}
					if (line.Range.Overlaps(other))
					{
						return true;
					}
				}
			}
			return false;
		}

		private static Line CreateAlignmentLine(PresentationContext ctx, string placeholder = "\n")
		{
			int cursor = ctx.Cursor;
			ctx.Append(placeholder);
			Line result = new Line(new Range(cursor, ctx.Cursor), ctx.LineNumber, null);
			ctx.LineNumber++;
			return result;
		}

		private static Line CreateView(PresentationContext ctx, MergeConflictPart viewMode, MergeConflict.Line line)
		{
			int cursor = ctx.Cursor;
			if (line is MergeConflict.EmptyLine && viewMode != MergeConflictPart.Merged)
			{
				ctx.Append("--- empty ---\n");
			}
			if (viewMode == MergeConflictPart.Merged)
			{
				ctx.Append(line.ResultString);
			}
			else
			{
				ctx.Append(line.OriginalString);
			}
			Line result = new Line(new Range(cursor, ctx.Cursor), ctx.LineNumber, line);
			ctx.LineNumber++;
			return result;
		}
	}
}
