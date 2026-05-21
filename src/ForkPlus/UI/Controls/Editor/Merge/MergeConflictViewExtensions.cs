using ForkPlus.Git.Merge;
using ForkPlus.Git.Merge.Presentation;

namespace ForkPlus.UI.Controls.Editor.Merge
{
	internal static class MergeConflictViewExtensions
	{
		public static MergeConflictView.Chunk GetConflictedChunkAt(this MergeConflictView _this, int offset)
		{
			MergeConflictView.Chunk[] chunks = _this.Chunks;
			foreach (MergeConflictView.Chunk chunk in chunks)
			{
				if (chunk.Node is MergeConflict.ConflictChunk && chunk.Range.Contains(offset))
				{
					return chunk;
				}
			}
			return null;
		}

		public static bool AllItemSelected(this MergeConflictView.Chunk _this, MergeConflictPart mode)
		{
			MergeConflictView.Line[] lines = _this.Lines;
			for (int i = 0; i < lines.Length; i++)
			{
				if (lines[i].Node is MergeConflict.SelectableLine { IsSelected: false })
				{
					return false;
				}
			}
			return true;
		}
	}
}
