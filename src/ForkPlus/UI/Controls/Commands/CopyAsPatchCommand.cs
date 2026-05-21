using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI.Controls.Editor.Diff;

namespace ForkPlus.UI.Controls.Commands
{
	public class CopyAsPatchCommand
	{
		public void AddMenuItems(DiffCodeEditor editor, ContextMenu menu)
		{
			menu.AddMenuItem("Copy as Patch", delegate
			{
				Execute(editor);
			}, null, new KeyGesture(Key.C, ModifierKeys.Control | ModifierKeys.Shift));
		}

		public static void Execute(DiffCodeEditor editor)
		{
			if (editor is CommitCodeEditor { VisualPatch: var visualPatch } commitCodeEditor)
			{
				if (visualPatch == null)
				{
					return;
				}
				int[] selectedPatchLines = commitCodeEditor.GetSelectedPatchLines();
				if (selectedPatchLines == null)
				{
					return;
				}
				Diff diff = ExtractPatch(visualPatch.VisualDiff.Node, selectedPatchLines);
				if (diff != null)
				{
					string text = new Patch(new Diff[1] { diff }).CreatePatchString();
					if (text != null)
					{
						ClipboardHelper.SetText(text);
					}
				}
				return;
			}
			VisualPatch visualPatch2 = editor.VisualPatch;
			if (visualPatch2 == null)
			{
				return;
			}
			Range viewCharRange = new Range(editor.SelectionStart, editor.SelectionStart + editor.SelectionLength);
			if (viewCharRange.Length <= 0)
			{
				return;
			}
			int[] patchLines = visualPatch2.VisualDiff.GetPatchLines(viewCharRange);
			Diff diff2 = ExtractPatch(visualPatch2.VisualDiff.Node, patchLines);
			if (diff2 != null)
			{
				string text2 = new Patch(new Diff[1] { diff2 }).CreatePatchString();
				if (text2 != null)
				{
					ClipboardHelper.SetText(text2);
				}
			}
		}

		[Null]
		private static Diff ExtractPatch(Diff diff, int[] linesToExtract)
		{
			List<string> list = new List<string>();
			List<Chunk> list2 = new List<Chunk>();
			Chunk[] chunks = diff.Chunks;
			foreach (Chunk chunk in chunks)
			{
				int num = 0;
				int num2 = 0;
				int? num3 = null;
				int? num4 = null;
				int num5 = 0;
				int num6 = 0;
				List<SubChunk> list3 = new List<SubChunk>();
				SubChunk[] subChunks = chunk.SubChunks;
				foreach (SubChunk subChunk in subChunks)
				{
					int count = list.Count;
					List<string> list4 = new List<string>();
					List<string> list5 = new List<string>();
					List<string> list6 = new List<string>();
					List<string> list7 = new List<string>();
					new List<int>();
					NoNewLineAtEndOfFile noNewLineAtEndOfFile = NoNewLineAtEndOfFile.None;
					for (int k = subChunk.PreContext.Start; k < subChunk.PreContext.End; k++)
					{
						num++;
						num2++;
						if (linesToExtract.ContainsItem(k))
						{
							if (!num3.HasValue)
							{
								num3 = chunk.FromStart + num - 1;
							}
							if (!num4.HasValue)
							{
								num4 = chunk.ToStart + num2 - 1;
							}
							list4.Add(diff.Lines[k]);
						}
					}
					for (int l = subChunk.Deleted.Start; l < subChunk.Deleted.End; l++)
					{
						num++;
						if (linesToExtract.ContainsItem(l))
						{
							if (!num3.HasValue)
							{
								num3 = chunk.FromStart + num - 1;
							}
							if (!num4.HasValue)
							{
								num4 = chunk.ToStart + num2 - 1;
							}
							bool num7 = (subChunk.NoNewLineAtEndOfFile & NoNewLineAtEndOfFile.Deleted) != 0 && l == subChunk.Deleted.End - 1;
							list5.Add(diff.Lines[l]);
							if (num7)
							{
								noNewLineAtEndOfFile |= NoNewLineAtEndOfFile.Deleted;
							}
						}
						else if (linesToExtract.Length != 0 && l >= linesToExtract.Min() && l <= linesToExtract.Max())
						{
							if (list6.Count == 0 && list5.Count == 0)
							{
								list4.Add(diff.Lines[l]);
							}
							else
							{
								list7.Add(diff.Lines[l]);
							}
						}
					}
					for (int m = subChunk.Added.Start; m < subChunk.Added.End; m++)
					{
						num2++;
						if (linesToExtract.ContainsItem(m))
						{
							if (!num3.HasValue)
							{
								num3 = chunk.FromStart + num - 1;
							}
							if (!num4.HasValue)
							{
								num4 = chunk.ToStart + num2 - 1;
							}
							bool num8 = (subChunk.NoNewLineAtEndOfFile & NoNewLineAtEndOfFile.Added) != 0 && m == subChunk.Added.End - 1;
							list6.Add(diff.Lines[m]);
							if (num8)
							{
								noNewLineAtEndOfFile |= NoNewLineAtEndOfFile.Added;
							}
						}
					}
					for (int n = subChunk.PostContext.Start; n < subChunk.PostContext.End; n++)
					{
						num++;
						num2++;
						if (!linesToExtract.ContainsItem(n))
						{
							continue;
						}
						if (!num3.HasValue)
						{
							num3 = chunk.FromStart + num - 1;
						}
						if (!num4.HasValue)
						{
							num4 = chunk.ToStart + num2 - 1;
						}
						bool flag = (subChunk.NoNewLineAtEndOfFile & NoNewLineAtEndOfFile.Context) != 0 && n == subChunk.PostContext.End - 1;
						if (list6.Count + list5.Count > 0)
						{
							list7.Add(diff.Lines[n]);
							if (flag)
							{
								noNewLineAtEndOfFile |= NoNewLineAtEndOfFile.Context;
							}
						}
						else
						{
							list4.Add(diff.Lines[n]);
						}
					}
					list.AddRange(list4);
					list.AddRange(list5);
					list.AddRange(list6);
					list.AddRange(list7);
					Range preContext = new Range(count, count + list4.Count);
					Range deleted = new Range(preContext.End, preContext.End + list5.Count);
					Range added = new Range(deleted.End, deleted.End + list6.Count);
					Range postContext = new Range(added.End, added.End + list7.Count);
					num5 += list4.Count + list7.Count + list5.Count;
					num6 += list4.Count + list7.Count + list6.Count;
					if (preContext.Length != 0 || deleted.Length != 0 || added.Length != 0 || postContext.Length != 0)
					{
						list3.Add(new SubChunk(preContext, deleted, added, postContext, noNewLineAtEndOfFile));
					}
				}
				if (list3.Count != 0)
				{
					list2.Add(new Chunk(Math.Max(num3 ?? chunk.FromStart, 0), num5, Math.Max(num4 ?? chunk.ToStart, 0), num6, chunk.ContextString, list3.ToArray()));
				}
			}
			if (list2.Count == 0)
			{
				return null;
			}
			return new Diff(diff.OldFilepath, diff.NewFilepath, diff.OldFileMode, diff.NewFileMode, diff.SrcObject, diff.DstObject, list.ToArray(), list2.ToArray(), diff.Similarity, diff.Type, diff.IsMinified);
		}
	}
}
