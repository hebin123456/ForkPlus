using ForkPlus.Git.Diff.Presentation;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	internal static class DiffViewExtensions
	{
		[Null]
		public static VisualChunk GetVisualChunkAt(this VisualDiff _this, int offset)
		{
			VisualChunk[] visualChunks = _this.VisualChunks;
			foreach (VisualChunk visualChunk in visualChunks)
			{
				if (visualChunk.CharRange.Contains(offset))
				{
					return visualChunk;
				}
			}
			return null;
		}

		public static int? GetGroupAt(this VisualChunk visualChunk, int characterOffset)
		{
			for (int i = 0; i < visualChunk.CustomHunks.Length; i++)
			{
				if (CustomHunkOverlaps(visualChunk.CustomHunks[i], visualChunk.VisualLines, characterOffset))
				{
					return i;
				}
			}
			return null;
		}

		private static bool CustomHunkOverlaps(Range lineRange, VisualLine[] visualLines, int characterOffset)
		{
			if (lineRange.Length == 1)
			{
				return visualLines[lineRange.Start].Range.Contains(characterOffset);
			}
			int start = visualLines[lineRange.Start].Range.Start;
			int end = visualLines[lineRange.End - 1].Range.End;
			return new Range(start, end).Contains(characterOffset);
		}
	}
}
