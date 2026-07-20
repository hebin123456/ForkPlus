namespace ForkPlus.Git.Diff
{
	public class Patch
	{
		public Diff[] Diffs { get; }

		public Patch(Diff[] diffs)
		{
			Diffs = diffs;
		}
	}
}
