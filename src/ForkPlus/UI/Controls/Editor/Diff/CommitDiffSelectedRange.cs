using ForkPlus.Git.Diff.Presentation;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public class CommitDiffSelectedRange
	{
		public VisualChunk VisualChunk;

		public int CustomHunkIndex;

		public CommitDiffSelectedRange(VisualChunk visualChunk, int customHunkIndex)
		{
			VisualChunk = visualChunk;
			CustomHunkIndex = customHunkIndex;
		}
	}
}
