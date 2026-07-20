using System;
using System.Collections.Generic;
using ForkPlus.Git.Diff.Parsing.Tokens;

namespace ForkPlus.Git.Merge.Presentation
{
	public static class MergeConflictViewExtensions
	{
		public static MergeConflictView.Line FindLine(this MergeConflictView view, int row)
		{
			MergeConflictView.Chunk[] chunks = view.Chunks;
			foreach (MergeConflictView.Chunk chunk in chunks)
			{
				if (!chunk.LineRange.Contains(row))
				{
					continue;
				}
				MergeConflictView.Line[] lines = chunk.Lines;
				foreach (MergeConflictView.Line line in lines)
				{
					if (line.LineNumber > row)
					{
						break;
					}
					if (line.LineNumber == row)
					{
						return line;
					}
				}
			}
			return null;
		}

		public static void RemoveRange(this MergeConflictView view, Range range)
		{
			if (range.Length == 0)
			{
				return;
			}
			Range range2 = range;
			MergeConflictView.Line[] lines = view.GetLines(range);
			MergeConflictView.Line[] array = lines;
			foreach (MergeConflictView.Line line in array)
			{
				MergeConflict.Line node = line.Node;
				if (node != null)
				{
					Log.Debug($"Old line  #{line.LineNumber} '{node.ResultString}'");
				}
			}
			bool flag = false;
			for (int j = 0; j < lines.Length; j++)
			{
				MergeConflictView.Line line2 = lines[j];
				MergeConflict.Line node2 = line2.Node;
				if (node2 == null)
				{
					Log.Error("Cannot remove alignment line");
					break;
				}
				if (j == 0)
				{
					range2 = new Range(SplitAt(line2.Range, range.Start).Item1.End, range.End);
				}
				if (j == lines.Length - 1 && flag && range2.Length == 0)
				{
					Log.Debug("reached remaining range length. Meld the last line into the first one");
					lines.FirstItem()?.MeldWith(line2);
					break;
				}
				int point = Math.Min(range2.End, line2.Range.End);
				(Range, Range) tuple = SplitAt(range2, point);
				Range item = tuple.Item1;
				Range item2 = tuple.Item2;
				bool flag2 = line2.Range.Start == item.Start && line2.Range.End == item.End;
				line2.RemoveSubrange(item);
				if (j == 0 && !flag2 && EndsWith(line2.Range, item) && lines.Length > 1)
				{
					Log.Info("removed \n. line merge required");
					flag = true;
				}
				Log.Info("new line: '" + node2.ResultString + "'");
				range2 = item2;
				if (j == lines.Length - 1 && flag && range2.Length == 0 && !flag2)
				{
					Log.Debug("Reached remaining range length. Meld the last line into the first one");
					lines.FirstItem()?.MeldWith(line2);
				}
			}
		}

		public static void Insert(this MergeConflictView view, int location, string stringToInsert)
		{
			if (stringToInsert.Length == 0)
			{
				return;
			}
			Log.Debug($"insert at {location} string: '{stringToInsert}'");
			MergeConflictView.Line line = view.GetLines(new Range(location, location))?.FirstItem();
			if (line != null)
			{
				MergeConflict.Line node = line.Node;
				if (node != null)
				{
					MergeConflict.Chunk parentChunk = node.ParentChunk;
					if (parentChunk == null)
					{
						Log.Error("Target line has no parent chunk");
						return;
					}
					int num = -1;
					for (int i = 0; i < parentChunk.ResultLines.Count; i++)
					{
						if (parentChunk.ResultLines[i] == node)
						{
							num = i;
							break;
						}
					}
					if (num == -1)
					{
						Log.Error("cannot find target line in the parent chunk");
						return;
					}
					MergeConflict.Line line2 = null;
					Range[] array = stringToInsert.LineRanges(includeEmptyLine: true);
					Log.Debug($"old line {line.LineNumber}: '{node.ResultString}'");
					for (int j = 0; j < array.Length; j++)
					{
						string text = stringToInsert.Substring(array[j]);
						if (j == 0)
						{
							line2 = line.Insert(text, location);
							if (j == array.Length - 1 && line2 != null)
							{
								line2.ParentChunk = parentChunk;
								parentChunk.ResultLines.Insert(num + 1, line2);
							}
							continue;
						}
						num++;
						if (j != array.Length - 1)
						{
							MergeConflict.Line line3 = new MergeConflict.Line(ContextType.None, text);
							line3.ParentChunk = parentChunk;
							parentChunk.ResultLines.Insert(num, line3);
							Log.Debug("new line: '" + text + "'");
							continue;
						}
						if (line2 == null)
						{
							Log.Error("tail is not defined");
							break;
						}
						line2.Insert(text, 0);
						line2.ParentChunk = parentChunk;
						parentChunk.ResultLines.Insert(num, line2);
						Log.Debug("new line: '" + line2.ResultString + "'");
					}
					return;
				}
			}
			Log.Error($"Can't find line with location {location}");
		}

		public static MergeConflictView.Line[] GetLines(this MergeConflictView view, Range range)
		{
			List<MergeConflictView.Line> list = new List<MergeConflictView.Line>();
			Range other = new Range(range.Start, range.End + 1);
			for (int i = 0; i < view.Chunks.Length; i++)
			{
				MergeConflictView.Chunk chunk = view.Chunks[i];
				if (chunk.Range.Start > other.End)
				{
					break;
				}
				if (!chunk.Range.Overlaps(other))
				{
					continue;
				}
				for (int j = 0; j < chunk.Lines.Length; j++)
				{
					MergeConflictView.Line line = chunk.Lines[j];
					if (line.Range.Start > other.End)
					{
						break;
					}
					if (line.Range.Overlaps(other))
					{
						list.Add(line);
					}
				}
			}
			if (list.Count == 0 && view.Chunks.Length > 1)
			{
				MergeConflictView.Chunk chunk2 = view.Chunks[view.Chunks.Length - 1];
				if (chunk2.Range.End == range.End && chunk2.Lines.Length != 0)
				{
					list.Add(chunk2.Lines[chunk2.Lines.Length - 1]);
				}
			}
			return list.ToArray();
		}

		private static (Range, Range) SplitAt(Range range, int point)
		{
			return (new Range(range.Start, point), new Range(point, Math.Max(point, range.End)));
		}

		private static bool EndsWith(Range range, Range otherRange)
		{
			if (!range.Contains(otherRange.Start))
			{
				return false;
			}
			return range.End == otherRange.End;
		}
	}
}
