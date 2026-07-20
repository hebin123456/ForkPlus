namespace ForkPlus.Git.Diff.Presentation
{
	public class PatchView
	{
		public Range Range { get; }

		public VisualDiff[] VisualDiffs { get; }

		public Patch Node { get; }

		public PatchView(Range range, VisualDiff[] visualDiffs, Patch node)
		{
			Range = range;
			VisualDiffs = visualDiffs;
			Node = node;
		}
	}
}
