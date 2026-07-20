using System;
using System.Collections.Generic;

namespace ForkPlus.Git.Diff.Presentation
{
	public class PatchHighlighter
	{
		internal void CreateDocumentHighlightScheme(VisualDiff oldVisualDiff, VisualDiff newVisualDiff, string oldStringValue, string newStringValue, out HighlightingScheme oldResult, out HighlightingScheme newResult)
		{
			List<Range> list = new List<Range>(64);
			List<Range> list2 = new List<Range>(64);
			List<Range> list3 = new List<Range>(64);
			List<Range> list4 = new List<Range>(64);
			List<Range> list5 = new List<Range>(64);
			List<Range> list6 = new List<Range>(64);
			List<Range> list7 = new List<Range>(64);
			List<Range> list8 = new List<Range>(64);
			List<Range> list9 = new List<Range>(64);
			List<Range> list10 = new List<Range>(64);
			List<Range> list11 = new List<Range>(64);
			List<Range> list12 = new List<Range>(64);
			for (int i = 0; i < oldVisualDiff.VisualChunks.Length; i++)
			{
				VisualChunk visualChunk = oldVisualDiff.VisualChunks[i];
				VisualChunk visualChunk2 = newVisualDiff.VisualChunks[i];
				Range? headerCharRange = visualChunk.HeaderCharRange;
				if (headerCharRange.HasValue)
				{
					Range valueOrDefault = headerCharRange.GetValueOrDefault();
					AddLine(valueOrDefault, list);
				}
				headerCharRange = visualChunk2.HeaderCharRange;
				if (headerCharRange.HasValue)
				{
					Range valueOrDefault2 = headerCharRange.GetValueOrDefault();
					AddLine(valueOrDefault2, list7);
				}
				VisualLine[] visualLines = visualChunk.VisualLines;
				foreach (VisualLine visualLine in visualLines)
				{
					switch (visualLine.Type)
					{
					case LineType.Deleted:
						AddLine(visualLine.Range, list3);
						break;
					case LineType.Added:
						AddLine(visualLine.Range, list2);
						break;
					case LineType.Alignment:
						AddLine(visualLine.Range, list4);
						break;
					case LineType.Pragma:
						AddLine(visualLine.Range, list);
						break;
					}
				}
				visualLines = visualChunk2.VisualLines;
				foreach (VisualLine visualLine2 in visualLines)
				{
					switch (visualLine2.Type)
					{
					case LineType.Deleted:
						AddLine(visualLine2.Range, list9);
						break;
					case LineType.Added:
						AddLine(visualLine2.Range, list8);
						break;
					case LineType.Alignment:
						AddLine(visualLine2.Range, list10);
						break;
					case LineType.Pragma:
						AddLine(visualLine2.Range, list7);
						break;
					}
				}
				for (int k = 0; k < visualChunk.VisualSubChunks.Length; k++)
				{
					VisualSubChunk visualSubChunk = visualChunk.VisualSubChunks[k];
					VisualSubChunk visualSubChunk2 = visualChunk2.VisualSubChunks[k];
					AddExtraHighlighting(visualChunk.VisualLines, visualChunk2.VisualLines, visualSubChunk.DeletedLines, visualSubChunk2.AddedLines, oldStringValue, newStringValue, list6, list11);
				}
			}
			oldResult = new HighlightingScheme(list.ToArray(), list3.ToArray(), list2.ToArray(), list4.ToArray(), list6.ToArray(), list5.ToArray());
			newResult = new HighlightingScheme(list7.ToArray(), list9.ToArray(), list8.ToArray(), list10.ToArray(), list12.ToArray(), list11.ToArray());
		}

		internal HighlightingScheme CreateDocumentHighlightScheme(VisualDiff visualDiff, string stringValue)
		{
			List<Range> list = new List<Range>(64);
			List<Range> list2 = new List<Range>(64);
			List<Range> list3 = new List<Range>(64);
			List<Range> list4 = new List<Range>(64);
			List<Range> list5 = new List<Range>(64);
			List<Range> list6 = new List<Range>(64);
			Range? headerCharRange = visualDiff.HeaderCharRange;
			if (headerCharRange.HasValue)
			{
				Range valueOrDefault = headerCharRange.GetValueOrDefault();
				AddLine(valueOrDefault, list);
			}
			VisualChunk[] visualChunks = visualDiff.VisualChunks;
			foreach (VisualChunk visualChunk in visualChunks)
			{
				headerCharRange = visualChunk.HeaderCharRange;
				if (headerCharRange.HasValue)
				{
					Range valueOrDefault2 = headerCharRange.GetValueOrDefault();
					AddLine(valueOrDefault2, list);
				}
				VisualLine[] visualLines = visualChunk.VisualLines;
				foreach (VisualLine visualLine in visualLines)
				{
					switch (visualLine.Type)
					{
					case LineType.Deleted:
						AddLine(visualLine.Range, list3);
						break;
					case LineType.Added:
						AddLine(visualLine.Range, list2);
						break;
					case LineType.Alignment:
						AddLine(visualLine.Range, list4);
						break;
					case LineType.Pragma:
						AddLine(visualLine.Range, list);
						break;
					}
				}
				VisualSubChunk[] visualSubChunks = visualChunk.VisualSubChunks;
				foreach (VisualSubChunk visualSubChunk in visualSubChunks)
				{
					AddExtraHighlighting(visualChunk.VisualLines, visualChunk.VisualLines, visualSubChunk.DeletedLines, visualSubChunk.AddedLines, stringValue, stringValue, list6, list5);
				}
			}
			return new HighlightingScheme(list.ToArray(), list3.ToArray(), list2.ToArray(), list4.ToArray(), list6.ToArray(), list5.ToArray());
		}

		private void AddExtraHighlighting(VisualLine[] oldVisualLines, VisualLine[] newVisualLines, Range deletedLines, Range addedLines, string removedText, string addedText, List<Range> extraRemoveRegions, List<Range> extraAddRegions)
		{
			if (deletedLines.Length == addedLines.Length && deletedLines.Length != 0)
			{
				for (int i = 0; i < addedLines.Length; i++)
				{
					HighlightDifference(removedText, addedText, oldVisualLines[deletedLines.Start + i], newVisualLines[addedLines.Start + i], extraRemoveRegions, extraAddRegions);
				}
			}
		}

		private void HighlightDifference(string removedText, string addedText, VisualLine removedLine, VisualLine addedLine, List<Range> extraRemoveRegions, List<Range> extraAddRegions)
		{
			int num = CommonLength(removedText, addedText, removedLine.Range, addedLine.Range, reversed: false);
			int num2 = CommonLength(removedText, addedText, new Range(removedLine.Range.Start + num, removedLine.Range.End), new Range(addedLine.Range.Start + num, addedLine.Range.End), reversed: true);
			extraRemoveRegions.Add(new Range(removedLine.Range.Start + num, removedLine.Range.End - num2));
			extraAddRegions.Add(new Range(addedLine.Range.Start + num, addedLine.Range.End - num2));
		}

		private int CommonLength(string removedText, string addedText, Range removedRange, Range addedRange, bool reversed)
		{
			int num = Math.Min(removedRange.Length, addedRange.Length);
			int num2 = (reversed ? (removedRange.End - 1) : removedRange.Start);
			int num3 = (reversed ? (addedRange.End - 1) : addedRange.Start);
			int num4 = ((!reversed) ? 1 : (-1));
			int i;
			for (i = 0; Math.Abs(i) < num && removedText[num2 + i] == addedText[num3 + i]; i += num4)
			{
			}
			return Math.Abs(i);
		}

		private static void AddLine(Range line, List<Range> destination)
		{
			destination.Add(new Range(line.Start, line.End - 1));
		}
	}
}
