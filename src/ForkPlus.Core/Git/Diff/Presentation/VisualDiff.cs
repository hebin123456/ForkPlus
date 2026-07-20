using System.Collections.Generic;

namespace ForkPlus.Git.Diff.Presentation
{
	public class VisualDiff
	{
		public DiffViewMode Mode { get; }

		public int LineCount { get; }

		public Range CharRange { get; }

		public Range? HeaderCharRange { get; }

		public VisualChunk[] VisualChunks { get; }

		public Diff Node { get; }

		public DiffLocation Location { get; }

		public VisualDiff(DiffViewMode mode, int lineCount, Range charRange, Range? headerCharRange, VisualChunk[] visualChunks, Diff node, DiffLocation location)
		{
			Mode = mode;
			LineCount = lineCount;
			CharRange = charRange;
			HeaderCharRange = headerCharRange;
			VisualChunks = visualChunks;
			Node = node;
			Location = location;
		}

		public int[] GetPatchLines(VisualChunk activeDstVisualChunk, int customBlockIndex, VisualDiff srcVisualDiff)
		{
			List<int> list = new List<int>();
			for (int i = 0; i < VisualChunks.Length; i++)
			{
				VisualChunk visualChunk = srcVisualDiff.VisualChunks[i];
				VisualChunk visualChunk2 = VisualChunks[i];
				if (visualChunk2 != activeDstVisualChunk)
				{
					continue;
				}
				Range range = visualChunk.CustomHunks[customBlockIndex];
				Range range2 = visualChunk2.CustomHunks[customBlockIndex];
				for (int j = range2.Start; j < range2.End; j++)
				{
					int nodeIndex = visualChunk2.VisualLines[j].NodeIndex;
					if (nodeIndex != -1)
					{
						list.Add(nodeIndex);
					}
				}
				for (int k = range.Start; k < range.End; k++)
				{
					int nodeIndex2 = visualChunk.VisualLines[k].NodeIndex;
					if (nodeIndex2 != -1)
					{
						list.Add(nodeIndex2);
					}
				}
			}
			list.Sort();
			return list.ToArray();
		}

		public int[] GetPatchLines(Range viewCharRange)
		{
			List<int> list = new List<int>();
			VisualChunk[] visualChunks = VisualChunks;
			foreach (VisualChunk visualChunk in visualChunks)
			{
				if (!visualChunk.CharRange.Overlaps(viewCharRange))
				{
					continue;
				}
				VisualSubChunk[] visualSubChunks = visualChunk.VisualSubChunks;
				foreach (VisualSubChunk visualSubChunk in visualSubChunks)
				{
					for (int k = visualSubChunk.PreContextLines.Start; k < visualSubChunk.PreContextLines.End; k++)
					{
						VisualLine visualLine = visualChunk.VisualLines[k];
						if (visualLine.Range.Overlaps(viewCharRange) && visualLine.NodeIndex != -1)
						{
							list.Add(visualLine.NodeIndex);
						}
					}
					for (int l = visualSubChunk.DeletedLines.Start; l < visualSubChunk.DeletedLines.End; l++)
					{
						VisualLine visualLine2 = visualChunk.VisualLines[l];
						if (visualLine2.Range.Overlaps(viewCharRange))
						{
							list.Add(visualLine2.NodeIndex);
						}
					}
					for (int m = visualSubChunk.AddedLines.Start; m < visualSubChunk.AddedLines.End; m++)
					{
						VisualLine visualLine3 = visualChunk.VisualLines[m];
						if (visualLine3.Range.Overlaps(viewCharRange))
						{
							list.Add(visualLine3.NodeIndex);
						}
					}
					for (int n = visualSubChunk.PostContextLines.Start; n < visualSubChunk.PostContextLines.End; n++)
					{
						VisualLine visualLine4 = visualChunk.VisualLines[n];
						if (visualLine4.Range.Overlaps(viewCharRange) && visualLine4.NodeIndex != -1)
						{
							list.Add(visualLine4.NodeIndex);
						}
					}
				}
			}
			return list.ToArray();
		}

		[Null]
		public Diff ConvertTo(ExtractPatchType type, int[] selectedLines)
		{
			Diff node = Node;
			List<string> list = new List<string>();
			List<Chunk> list2 = new List<Chunk>();
			Chunk[] chunks = node.Chunks;
			foreach (Chunk chunk in chunks)
			{
				int num = 0;
				int num2 = 0;
				List<SubChunk> list3 = new List<SubChunk>(chunk.SubChunks.Length);
				SubChunk[] subChunks = chunk.SubChunks;
				foreach (SubChunk subChunk in subChunks)
				{
					int count = list.Count;
					List<string> list4 = new List<string>();
					List<string> list5 = new List<string>();
					List<string> list6 = new List<string>();
					List<string> list7 = new List<string>();
					NoNewLineAtEndOfFile noNewLineAtEndOfFile = NoNewLineAtEndOfFile.None;
					for (int k = subChunk.PreContext.Start; k < subChunk.PreContext.End; k++)
					{
						string item = node.Lines[k];
						list4.Add(item);
					}
					for (int l = subChunk.Deleted.Start; l < subChunk.Deleted.End; l++)
					{
						string item2 = node.Lines[l];
						bool flag = (subChunk.NoNewLineAtEndOfFile & NoNewLineAtEndOfFile.Deleted) != 0 && l == subChunk.Deleted.End - 1;
						if (selectedLines.ContainsItem(l))
						{
							if (type == ExtractPatchType.Discard)
							{
								list6.Add(item2);
								if (flag)
								{
									noNewLineAtEndOfFile |= NoNewLineAtEndOfFile.Added;
								}
							}
							else
							{
								list5.Add(item2);
								if (flag)
								{
									noNewLineAtEndOfFile |= NoNewLineAtEndOfFile.Deleted;
								}
							}
						}
						else
						{
							if (type != 0)
							{
								continue;
							}
							if (list6.Count == 0 && list5.Count == 0)
							{
								list4.Add(item2);
								continue;
							}
							list7.Add(item2);
							if (flag)
							{
								noNewLineAtEndOfFile |= NoNewLineAtEndOfFile.Context;
							}
						}
					}
					for (int m = subChunk.Added.Start; m < subChunk.Added.End; m++)
					{
						string item3 = node.Lines[m];
						bool flag2 = (subChunk.NoNewLineAtEndOfFile & NoNewLineAtEndOfFile.Added) != 0 && m == subChunk.Added.End - 1;
						if (selectedLines.ContainsItem(m))
						{
							if (type == ExtractPatchType.Discard)
							{
								list5.Add(item3);
								if (flag2)
								{
									noNewLineAtEndOfFile |= NoNewLineAtEndOfFile.Deleted;
								}
							}
							else
							{
								list6.Add(item3);
								if (flag2)
								{
									noNewLineAtEndOfFile |= NoNewLineAtEndOfFile.Added;
								}
							}
						}
						else
						{
							if (type != ExtractPatchType.Unstage && type != ExtractPatchType.Discard)
							{
								continue;
							}
							if (list6.Count == 0 && list5.Count == 0)
							{
								list4.Add(item3);
								continue;
							}
							list7.Add(item3);
							if (flag2)
							{
								noNewLineAtEndOfFile |= NoNewLineAtEndOfFile.Context;
							}
						}
					}
					if (list6.Count + list5.Count > 0)
					{
						for (int n = subChunk.PostContext.Start; n < subChunk.PostContext.End; n++)
						{
							string item4 = node.Lines[n];
							list7.Add(item4);
						}
					}
					else
					{
						for (int num3 = subChunk.PostContext.Start; num3 < subChunk.PostContext.End; num3++)
						{
							string item5 = node.Lines[num3];
							list4.Add(item5);
						}
					}
					list.AddRange(list4);
					list.AddRange(list5);
					list.AddRange(list6);
					list.AddRange(list7);
					Range preContext = new Range(count, count + list4.Count);
					Range deleted = new Range(preContext.End, preContext.End + list5.Count);
					Range added = new Range(deleted.End, deleted.End + list6.Count);
					SubChunk subChunk2 = new SubChunk(postContext: new Range(added.End, added.End + list7.Count), preContext: preContext, deleted: deleted, added: added, noNewLineAtEndOfFile: noNewLineAtEndOfFile);
					num += subChunk2.PreContext.Length + subChunk2.PostContext.Length + subChunk2.Deleted.Length;
					num2 += subChunk2.PreContext.Length + subChunk2.PostContext.Length + subChunk2.Added.Length;
					list3.Add(subChunk2);
				}
				if (list3.AnyItem((SubChunk x) => x.Deleted.Length > 0 || x.Added.Length > 0))
				{
					list2.Add(new Chunk(chunk.FromStart, num, chunk.ToStart, num2, chunk.ContextString, list3.ToArray()));
				}
			}
			if (list2.Count == 0)
			{
				return null;
			}
			string oldFilepath = ((type != ExtractPatchType.Unstage || node.OldFilepath != null) ? node.OldFilepath : node.NewFilepath);
			_ = 1;
			return new Diff(oldFilepath, node.NewFilepath, node.OldFileMode, node.NewFileMode, node.SrcObject, node.DstObject, list.ToArray(), list2.ToArray(), node.Similarity, node.Type, node.IsMinified);
		}
	}
}
