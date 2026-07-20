using ForkPlus.Services;
using System.Text;

namespace ForkPlus.Git.Diff
{
	public static class PatchExtensions
	{
		public static int LinesCount(this Patch patch)
		{
			int num = 0;
			Diff[] diffs = patch.Diffs;
			for (int i = 0; i < diffs.Length; i++)
			{
				Chunk[] chunks = diffs[i].Chunks;
				for (int j = 0; j < chunks.Length; j++)
				{
					SubChunk[] subChunks = chunks[j].SubChunks;
					foreach (SubChunk subChunk in subChunks)
					{
						num += subChunk.Deleted.Length;
						num += subChunk.Added.Length;
					}
				}
			}
			return num;
		}

		[Null]
		public static byte[] CreatePatchData(this Patch patch)
		{
			Diff[] diffs = patch.Diffs;
			int num = 0;
			if (num < diffs.Length)
			{
				return diffs[num].CreatePatchData();
			}
			return null;
		}

		[Null]
		public static string CreatePatchString(this Patch patch)
		{
			Diff[] diffs = patch.Diffs;
			int num = 0;
			if (num < diffs.Length)
			{
				return diffs[num].CreatePatchString();
			}
			return null;
		}

		private static byte[] CreatePatchData(this Diff diff)
		{
			PresentationContext presentationContext = new PresentationContext();
			ExtractHeader(presentationContext, diff);
			string s = presentationContext.ResultString();
			byte[] bytes = Encoding.UTF8.GetBytes(s);
			PresentationContext presentationContext2 = new PresentationContext();
			ExtractBody(presentationContext2, diff);
			string s2 = presentationContext2.ResultString();
			byte[] bytes2 = Encoding.UTF8.GetBytes(s2);
			string s3 = "\n--\n" + ServiceLocator.AppContext.UserAgent;
			byte[] bytes3 = Encoding.UTF8.GetBytes(s3);
			byte[] array = new byte[bytes.Length + bytes2.Length + bytes3.Length];
			bytes.CopyTo(array, 0);
			bytes2.CopyTo(array, bytes.Length);
			bytes3.CopyTo(array, bytes.Length + bytes2.Length);
			return array;
		}

		private static string CreatePatchString(this Diff diff)
		{
			PresentationContext presentationContext = new PresentationContext();
			bool showFileIndex = !diff.OldFileMode.HasValue;
			ExtractHeader(presentationContext, diff, showFileIndex);
			ExtractBody(presentationContext, diff);
			return presentationContext.ResultString();
		}

		private static void ExtractBody(PresentationContext ctx, Diff diff)
		{
			Chunk[] chunks = diff.Chunks;
			foreach (Chunk chunk in chunks)
			{
				AsUnifiedDiffString(ctx, diff.Lines, chunk);
			}
		}

		private static void ExtractHeader(PresentationContext ctx, Diff diff, bool showFileIndex = true)
		{
			string text = diff.OldFilepath ?? diff.NewFilepath;
			string text2 = diff.NewFilepath ?? diff.OldFilepath;
			if (text == null || text2 == null)
			{
				Log.Error("OldFileName and NewFileName in Diff are not defined.");
			}
			else
			{
				ctx.Append("diff --git a/" + text + " b/" + text2 + "\n");
			}
			if (showFileIndex)
			{
				if (diff.OldFilepath == null)
				{
					ctx.Append("new file mode 100000\n");
				}
				if (diff.NewFilepath == null)
				{
					string text3 = (diff.OldFileMode ?? new FileMode(10644)).AsString();
					ctx.Append("deleted file mode " + text3 + "\n");
				}
				string srcObject = diff.SrcObject;
				if (srcObject != null)
				{
					string dstObject = diff.DstObject;
					if (dstObject != null)
					{
						ctx.Append("index " + srcObject + ".." + dstObject + " " + (diff.OldFileMode ?? diff.NewFileMode).Value.AsString() + "\n");
					}
				}
			}
			if (diff.OldFilepath != null)
			{
				ctx.Append("--- a/" + text + "\n");
			}
			else
			{
				ctx.Append("--- /dev/null\n");
			}
			if (diff.NewFilepath != null)
			{
				ctx.Append("+++ b/" + diff.NewFilepath + "\n");
			}
			else
			{
				ctx.Append("+++ /dev/null\n");
			}
		}

		private static void AsUnifiedDiffString(PresentationContext ctx, string[] lines, Chunk chunk)
		{
			ctx.Append($"@@ -{chunk.FromStart},{chunk.FromLength} +{chunk.ToStart},{chunk.ToLength} @@\n");
			ctx.LineNumber++;
			SubChunk[] subChunks = chunk.SubChunks;
			foreach (SubChunk subChunk in subChunks)
			{
				AsUnifiedDiffString(ctx, lines, subChunk);
			}
		}

		private static void AsUnifiedDiffString(PresentationContext ctx, string[] lines, SubChunk subChunk)
		{
			for (int i = subChunk.PreContext.Start; i < subChunk.PreContext.End; i++)
			{
				ctx.Append(' ');
				ctx.Append(lines[i]);
				ctx.LineNumber++;
			}
			for (int j = subChunk.Deleted.Start; j < subChunk.Deleted.End; j++)
			{
				ctx.Append('-');
				ctx.Append(lines[j]);
				ctx.LineNumber++;
				if ((subChunk.NoNewLineAtEndOfFile & NoNewLineAtEndOfFile.Deleted) != 0 && j == subChunk.Deleted.End - 1)
				{
					ctx.Append("\\ No newline at end of file\n");
					ctx.LineNumber++;
				}
			}
			for (int k = subChunk.Added.Start; k < subChunk.Added.End; k++)
			{
				ctx.Append('+');
				ctx.Append(lines[k]);
				ctx.LineNumber++;
				if ((subChunk.NoNewLineAtEndOfFile & NoNewLineAtEndOfFile.Added) != 0 && k == subChunk.Added.End - 1)
				{
					ctx.Append("\\ No newline at end of file\n");
					ctx.LineNumber++;
				}
			}
			for (int l = subChunk.PostContext.Start; l < subChunk.PostContext.End; l++)
			{
				ctx.Append(' ');
				ctx.Append(lines[l]);
				ctx.LineNumber++;
				if ((subChunk.NoNewLineAtEndOfFile & NoNewLineAtEndOfFile.Context) != 0 && l == subChunk.PostContext.End - 1)
				{
					ctx.Append("\\ No newline at end of file\n");
					ctx.LineNumber++;
				}
			}
		}
	}
}
