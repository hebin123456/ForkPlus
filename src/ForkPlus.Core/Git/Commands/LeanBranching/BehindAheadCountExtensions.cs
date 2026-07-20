namespace ForkPlus.Git.Commands.LeanBranching
{
	public static class BehindAheadCountExtensions
	{
		public static bool AreInSync(this BehindAheadCount behindAheadCount)
		{
			if (behindAheadCount.Left != 0)
			{
				return behindAheadCount.Right == 0;
			}
			return true;
		}
	}
}
