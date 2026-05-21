using System.Collections.Generic;
using ForkPlus.Biturbo;

namespace ForkPlus.Git.Commands
{
	internal class GetBehindAheadCountGitCommand
	{
		public GitCommandResult<BehindAheadCount> Execute(GitModule gitModule, Sha left, Sha right, CommitGraphCache commitGraphCache)
		{
			Benchmarker benchmarker = new Benchmarker("GetBehindAheadCountGitCommand " + left.ToAbbreviatedString() + " " + right.ToAbbreviatedString());
			(Sha, Sha) tuple = (left, right);
			if (tuple.Item1 == tuple.Item2)
			{
				benchmarker.LogElapsed();
				return GitCommandResult<BehindAheadCount>.Success(new BehindAheadCount(0, 0));
			}
			List<BtOidPair> list = new List<BtOidPair>();
			list.Add(new BtOidPair
			{
				left = tuple.Item1.ToBtOid(),
				right = tuple.Item2.ToBtOid()
			});
			BtOidPair[] btOidPairs = list.ToArray();
			BtCommitGraphCache btCommitGraphCache = commitGraphCache.Handle;
			GitCommandResult<BehindAheadCount> result = BtRequest.Run(() => default(BtBehindAheadCounts), delegate(ref BtBehindAheadCounts x)
			{
				return Bt.bt_get_behind_ahead_counts(gitModule.GitDir(), btOidPairs, btOidPairs.Length, ref btCommitGraphCache, ref x);
			}, delegate(ref BtBehindAheadCounts x)
			{
				return x.Into(left, right);
			}, delegate(ref BtBehindAheadCounts x)
			{
				Bt.bt_release_behind_ahead_counts(ref x);
			});
			benchmarker.LogElapsed();
			return result;
		}
	}
}
