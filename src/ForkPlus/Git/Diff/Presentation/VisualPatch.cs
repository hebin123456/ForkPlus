using System;
using System.Collections.Generic;
using System.Text;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.Git.Diff.Presentation
{
	public class VisualPatch
	{
		private class PresentationContext
		{
			public int Cursor;

			public int LineNumber;

			public DiffViewMode ViewMode { get; }

			public bool EntireFile { get; }

			public StringBuilder StringBuilder { get; }

			public PresentationContext(DiffViewMode viewMode, bool entireFile)
			{
				ViewMode = viewMode;
				EntireFile = entireFile;
				StringBuilder = new StringBuilder(1024);
				Cursor = 0;
				LineNumber = 0;
			}

			public void Append(string input)
			{
				StringBuilder.Append(input);
				Cursor += input.Length;
			}
		}

		private static PatchHighlighter PatchHighlighter = new PatchHighlighter();

		public string StringValue { get; }

		public VisualDiff VisualDiff { get; }

		public HighlightingScheme HighlightingScheme { get; }

		public VisualPatch(string stringValue, VisualDiff visualDiff, HighlightingScheme highlightingScheme)
		{
			StringValue = stringValue;
			VisualDiff = visualDiff;
			HighlightingScheme = highlightingScheme;
		}

		[Null]
		public static VisualPatch CreateVisualPatch([Null] Diff diff, bool entireFile, DiffLocation location)
		{
			if (diff == null)
			{
				return null;
			}
			PresentationContext presentationContext = new PresentationContext(DiffViewMode.Split, entireFile);
			VisualDiff visualDiff = CreateVisualDiff(presentationContext, diff, location);
			string stringValue = presentationContext.StringBuilder.ToString();
			HighlightingScheme highlightingScheme = PatchHighlighter.CreateDocumentHighlightScheme(visualDiff, stringValue);
			return new VisualPatch(stringValue, visualDiff, highlightingScheme);
		}

		public static void CreateSideBySideVisualPatch([Null] Diff diff, bool entireFile, DiffLocation location, [Null] out VisualPatch old, [Null] out VisualPatch @new)
		{
			if (diff == null)
			{
				old = null;
				@new = null;
				return;
			}
			PresentationContext presentationContext = new PresentationContext(DiffViewMode.SideBySideOld, entireFile);
			VisualDiff visualDiff = CreateVisualDiff(presentationContext, diff, location);
			string text = presentationContext.StringBuilder.ToString();
			PresentationContext presentationContext2 = new PresentationContext(DiffViewMode.SideBySideNew, entireFile);
			VisualDiff visualDiff2 = CreateVisualDiff(presentationContext2, diff, location);
			string text2 = presentationContext2.StringBuilder.ToString();
			PatchHighlighter.CreateDocumentHighlightScheme(visualDiff, visualDiff2, text, text2, out var oldResult, out var newResult);
			old = new VisualPatch(text, visualDiff, oldResult);
			@new = new VisualPatch(text2, visualDiff2, newResult);
		}

		private static VisualDiff CreateVisualDiff(PresentationContext ctx, Diff diff, DiffLocation location)
		{
			int cursor = ctx.Cursor;
			int lineNumber = ctx.LineNumber;
			Range? headerCharRange = null;
			if (diff.OldFileMode.HasValue && diff.NewFileMode.HasValue && diff.OldFileMode != diff.NewFileMode)
			{
				ctx.Append(" changed file mode " + diff.OldFileMode.Value.AsString() + " → " + diff.NewFileMode.Value.AsString() + "\n");
				ctx.LineNumber++;
				headerCharRange = new Range(cursor, ctx.Cursor);
			}
			List<VisualChunk> list = new List<VisualChunk>(diff.Chunks.Length);
			Chunk[] chunks = diff.Chunks;
			foreach (Chunk chunk in chunks)
			{
				list.Add(CreateVisualChunk(ctx, diff, chunk));
			}
			return new VisualDiff(ctx.ViewMode, ctx.LineNumber - lineNumber, new Range(cursor, ctx.Cursor), headerCharRange, list.ToArray(), diff, location);
		}

		private static VisualChunk CreateVisualChunk(PresentationContext ctx, Diff diff, Chunk chunk)
		{
			int cursor = ctx.Cursor;
			Range? headerCharRange = null;
			if (!ctx.EntireFile)
			{
				ctx.Append($"@@ -{chunk.FromStart},{chunk.FromLength} +{chunk.ToStart},{chunk.ToLength} @@");
				string contextString = chunk.ContextString;
				if (contextString != null)
				{
					ctx.Append(" " + contextString);
				}
				ctx.Append("\n");
				headerCharRange = new Range(cursor, ctx.Cursor);
				ctx.LineNumber++;
			}
			int cursor2 = ctx.Cursor;
			int lineNumber = ctx.LineNumber;
			List<VisualLine> list = new List<VisualLine>();
			List<VisualSubChunk> list2 = new List<VisualSubChunk>(chunk.SubChunks.Length);
			SubChunk[] subChunks = chunk.SubChunks;
			foreach (SubChunk subChunk in subChunks)
			{
				list2.Add(CreateVisualSubChunk(ctx, diff, subChunk, list));
			}
			List<Range> list3 = new List<Range>();
			int cursor3 = 0;
			while (cursor3 < list.Count)
			{
				Range? range = ReadCustomHunk(list, ref cursor3);
				if (range.HasValue)
				{
					Range valueOrDefault = range.GetValueOrDefault();
					list3.Add(valueOrDefault);
				}
			}
			return new VisualChunk(new Range(cursor, ctx.Cursor), list3.ToArray(), headerCharRange, list.ToArray(), new Range(lineNumber, ctx.LineNumber), new Range(cursor2, ctx.Cursor), list2.ToArray(), chunk);
		}

		private static Range? ReadCustomHunk(List<VisualLine> lines, ref int cursor)
		{
			int num = 2;
			int num2 = 0;
			int val = cursor;
			int? num3 = null;
			int? num4 = null;
			while (cursor < lines.Count)
			{
				VisualLine visualLine = lines[cursor];
				switch (num2)
				{
				case 0:
					if (visualLine.Type == LineType.Deleted || visualLine.Type == LineType.Added || visualLine.Type == LineType.Alignment)
					{
						num3 = Math.Max(val, cursor - num);
						num2 = 1;
					}
					break;
				case 1:
					if (visualLine.Type == LineType.Context)
					{
						num4 = cursor;
						num2 = 2;
					}
					break;
				case 2:
					if (visualLine.Type == LineType.Context)
					{
						if (cursor - num4.Value + 1 == num * 2)
						{
							cursor -= num - 1;
							return new Range(num3.Value, cursor);
						}
					}
					else if (visualLine.Type == LineType.Deleted || visualLine.Type == LineType.Added || visualLine.Type == LineType.Alignment)
					{
						num2 = 1;
						num4 = null;
					}
					break;
				}
				cursor++;
			}
			return num2 switch
			{
				1 => new Range(num3.Value, cursor), 
				2 => new Range(num3.Value, Math.Min(num4.Value + num, cursor)), 
				_ => null, 
			};
		}

		private static VisualSubChunk CreateVisualSubChunk(PresentationContext ctx, Diff diff, SubChunk subChunk, List<VisualLine> visualLines)
		{
			int cursor = ctx.Cursor;
			List<int> list = new List<int>();
			int count = visualLines.Count;
			int num = count;
			for (int i = subChunk.PreContext.Start; i < subChunk.PreContext.End; i++)
			{
				string stringValue = diff.Lines[i];
				visualLines.Add(CreateVisualLine(ctx, LineType.Context, stringValue, i));
				num++;
			}
			// 修复（2026-09-14，"FileDiff SideBySide 左右不严格对齐"）：
			// "\ No newline at end of file" pragma 行只加在所属一侧（Deleted→左 / Added→右），
			// 而 Alignment 空行补偿只按 Deleted/Added 行数差计算、未计入 pragma 行，
			// 导致带单侧标记的 diff 左右行数差 1，之后所有行错位一行。
			// 修复：change 块两侧总行数统一按 max(Deleted+pragmaD, Added+pragmaA) 对齐，
			// 短的一侧在块尾补 Alignment 空行（无 pragma 时与原 max(0, A-D)/max(0, D-A) 完全等价）。
			bool flag = (subChunk.NoNewLineAtEndOfFile & NoNewLineAtEndOfFile.Deleted) != 0;
			bool flag2 = (subChunk.NoNewLineAtEndOfFile & NoNewLineAtEndOfFile.Added) != 0;
			int num5 = subChunk.Deleted.Length + (flag ? 1 : 0);
			int num6 = subChunk.Added.Length + (flag2 ? 1 : 0);
			int count2 = visualLines.Count;
			int num2 = count2;
			if (ctx.ViewMode != DiffViewMode.SideBySideNew)
			{
				for (int j = subChunk.Deleted.Start; j < subChunk.Deleted.End; j++)
				{
					string stringValue2 = diff.Lines[j];
					visualLines.Add(CreateVisualLine(ctx, LineType.Deleted, stringValue2, j));
					if (flag && j == subChunk.Deleted.End - 1)
					{
						list.Add(visualLines.Count);
						visualLines.Add(CreateNoNewlineAtEndOfFilePragmaVisualLine(ctx));
					}
				}
				num2 += subChunk.Deleted.Length;
			}
			int count3 = visualLines.Count;
			int num3 = count3;
			if (ctx.ViewMode != 0)
			{
				for (int k = subChunk.Added.Start; k < subChunk.Added.End; k++)
				{
					string stringValue3 = diff.Lines[k];
					visualLines.Add(CreateVisualLine(ctx, LineType.Added, stringValue3, k));
					if (flag2 && k == subChunk.Added.End - 1)
					{
						list.Add(visualLines.Count);
						visualLines.Add(CreateNoNewlineAtEndOfFilePragmaVisualLine(ctx));
					}
				}
				num3 += subChunk.Added.Length;
			}
			if (ctx.ViewMode == DiffViewMode.SideBySideOld)
			{
				int num7 = Math.Max(num5, num6) - num5;
				for (int l = 0; l < num7; l++)
				{
					visualLines.Add(CreateAlignmentVisualLine(ctx));
				}
			}
			else if (ctx.ViewMode == DiffViewMode.SideBySideNew)
			{
				int num8 = Math.Max(num5, num6) - num6;
				for (int m = 0; m < num8; m++)
				{
					visualLines.Add(CreateAlignmentVisualLine(ctx));
				}
			}
			int count4 = visualLines.Count;
			int num4 = count4;
			for (int n = subChunk.PostContext.Start; n < subChunk.PostContext.End; n++)
			{
				string stringValue4 = diff.Lines[n];
				visualLines.Add(CreateVisualLine(ctx, LineType.Context, stringValue4, n));
				// 修复（2026-09-14）：末行 context 无换行的标记解析到的是 Context 位（见
				// PatchParser.ParseSubChunk），此处原误查 Deleted 位导致 SideBySide/Split
				// 视图静默丢弃该 "\ No newline" 提示；与 PatchExtensions（Split 权威渲染）对齐。
				// 该 pragma 两侧同步追加，不影响左右行数对齐。
				if ((subChunk.NoNewLineAtEndOfFile & NoNewLineAtEndOfFile.Context) != 0 && n == subChunk.PostContext.End - 1)
				{
					list.Add(visualLines.Count);
					visualLines.Add(CreateNoNewlineAtEndOfFilePragmaVisualLine(ctx));
				}
				num4++;
			}
			return new VisualSubChunk(new Range(cursor, ctx.Cursor), new Range(count, num), new Range(count2, num2), new Range(count3, num3), new Range(count4, num4), list.ToArray(), subChunk);
		}

		private static VisualLine CreateNoNewlineAtEndOfFilePragmaVisualLine(PresentationContext ctx)
		{
			return CreateVisualLine(ctx, LineType.Pragma, PreferencesLocalization.Translate("No newline at end of file", ForkPlusSettings.Default.UiLanguage) + "\n", -1);
		}

		private static VisualLine CreateAlignmentVisualLine(PresentationContext ctx)
		{
			return CreateVisualLine(ctx, LineType.Alignment, "\n", -1);
		}

		private static VisualLine CreateVisualLine(PresentationContext ctx, LineType type, string stringValue, int nodeIndex)
		{
			int cursor = ctx.Cursor;
			ctx.Append(stringValue);
			VisualLine result = new VisualLine(type, new Range(cursor, ctx.Cursor), ctx.LineNumber, nodeIndex);
			ctx.LineNumber++;
			return result;
		}
	}
}
