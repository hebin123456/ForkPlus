using System;
using System.Collections.Generic;
using ForkPlus.Settings;

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
			if (ForkPlusSettings.Default.DiffWordLevelHighlight)
			{
				HighlightWordLevelDifference(removedText, addedText, removedLine, addedLine, extraRemoveRegions, extraAddRegions);
			}
			else
			{
				int num = CommonLength(removedText, addedText, removedLine.Range, addedLine.Range, reversed: false);
				int num2 = CommonLength(removedText, addedText, new Range(removedLine.Range.Start + num, removedLine.Range.End), new Range(addedLine.Range.Start + num, addedLine.Range.End), reversed: true);
				extraRemoveRegions.Add(new Range(removedLine.Range.Start + num, removedLine.Range.End - num2));
				extraAddRegions.Add(new Range(addedLine.Range.Start + num, addedLine.Range.End - num2));
			}
		}

		// Word-level LCS diff: tokenize each line (skipping the leading +/-/space prefix
		// and trailing \n included in VisualLine.Range), compute LCS over the token
		// sequences, then emit the non-LCS token runs as extra highlight regions.
		// Downstream (HighlightingScheme.Range[] + DiffBackgroundColorizer ExactAdd/ExactRemove)
		// already supports per-segment geometry, so multiple regions per line render correctly.
		private void HighlightWordLevelDifference(string removedText, string addedText, VisualLine removedLine, VisualLine addedLine, List<Range> extraRemoveRegions, List<Range> extraAddRegions)
		{
			Range rmRange = removedLine.Range;
			Range addRange = addedLine.Range;
			if (rmRange.Length < 3 || addRange.Length < 3)
			{
				return;
			}
			int rmStart = rmRange.Start + 1;
			int rmEnd = rmRange.End - 1;
			int addStart = addRange.Start + 1;
			int addEnd = addRange.End - 1;
			if (rmEnd <= rmStart || addEnd <= addStart)
			{
				return;
			}
			List<Token> rmTokens = Tokenize(removedText, rmStart, rmEnd);
			List<Token> addTokens = Tokenize(addedText, addStart, addEnd);
			if (rmTokens.Count == 0 || addTokens.Count == 0)
			{
				return;
			}
			if (rmTokens.Count > 500 || addTokens.Count > 500)
			{
				return;
			}
			AddWordLevelDiffRegions(rmTokens, addTokens, extraRemoveRegions, extraAddRegions);
		}

		private struct Token
		{
			public int Start;

			public int Length;

			public string Text;
		}

		private static List<Token> Tokenize(string text, int start, int end)
		{
			List<Token> list = new List<Token>(end - start);
			int i = start;
			while (i < end)
			{
				if (IsWordChar(text[i]))
				{
					int j = i + 1;
					while (j < end && IsWordChar(text[j]))
					{
						j++;
					}
					list.Add(new Token
					{
						Start = i,
						Length = j - i,
						Text = text.Substring(i, j - i)
					});
					i = j;
				}
				else
				{
					list.Add(new Token
					{
						Start = i,
						Length = 1,
						Text = text.Substring(i, 1)
					});
					i++;
				}
			}
			return list;
		}

		private static bool IsWordChar(char c)
		{
			return char.IsLetterOrDigit(c) || c == '_';
		}

		private static void AddWordLevelDiffRegions(List<Token> aTokens, List<Token> bTokens, List<Range> aRegions, List<Range> bRegions)
		{
			int m = aTokens.Count;
			int n = bTokens.Count;
			int[,] array = new int[m + 1, n + 1];
			for (int i = 1; i <= m; i++)
			{
				for (int j = 1; j <= n; j++)
				{
					if (aTokens[i - 1].Text == bTokens[j - 1].Text)
					{
						array[i, j] = array[i - 1, j - 1] + 1;
					}
					else
					{
						array[i, j] = Math.Max(array[i - 1, j], array[i, j - 1]);
					}
				}
			}
			bool[] array2 = new bool[m];
			bool[] array3 = new bool[n];
			int num = m;
			int num2 = n;
			while (num > 0 && num2 > 0)
			{
				if (aTokens[num - 1].Text == bTokens[num2 - 1].Text)
				{
					array2[num - 1] = true;
					array3[num2 - 1] = true;
					num--;
					num2--;
				}
				else if (array[num - 1, num2] >= array[num, num2 - 1])
				{
					num--;
				}
				else
				{
					num2--;
				}
			}
			AddDiffRegions(aTokens, array2, aRegions);
			AddDiffRegions(bTokens, array3, bRegions);
		}

		private static void AddDiffRegions(List<Token> tokens, bool[] inLcs, List<Range> regions)
		{
			int i = 0;
			while (i < tokens.Count)
			{
				if (inLcs[i])
				{
					i++;
					continue;
				}
				int start = tokens[i].Start;
				int end = tokens[i].Start + tokens[i].Length;
				int j = i + 1;
				while (j < tokens.Count && !inLcs[j])
				{
					end = tokens[j].Start + tokens[j].Length;
					j++;
				}
				if (end > start)
				{
					regions.Add(new Range(start, end));
				}
				i = j;
			}
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
