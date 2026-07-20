using ForkPlus.Biturbo;

namespace ForkPlus.Git.Commands
{
	internal static class BtBehindAheadCountsExtensions
	{
		public static GitCommandResult<BehindAheadCount> Into(this ref BtBehindAheadCounts btBehindAheadCounts, Sha left, Sha right)
		{
			BehindAheadCount[] structArray = btBehindAheadCounts.items.GetStructArray(btBehindAheadCounts.items_len, (BtBehindAheadCount btBehindAheadCount) => new BehindAheadCount((int)btBehindAheadCount.left, (int)btBehindAheadCount.right));
			if (structArray.Length == 0)
			{
				return GitCommandResult<BehindAheadCount>.Failure(new GitCommandError.Bug($"Failed to get behind-ahead between '{left}' and '{right}'"));
			}
			return GitCommandResult<BehindAheadCount>.Success(structArray[0]);
		}
	}
}
