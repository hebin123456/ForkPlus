using System.Collections.Generic;

namespace ForkPlus.Git
{
	public class UpstreamStatusCache
	{
		public static readonly UpstreamStatusCache Empty = new UpstreamStatusCache(new Dictionary<string, UpstreamStatus>(), new Dictionary<Sha, ActiveBranchCommitStatus>());

		private readonly Dictionary<string, UpstreamStatus> _upstreamStatusCache;

		public UpstreamStatusCache(Dictionary<string, UpstreamStatus> upstreamStatusCache, Dictionary<Sha, ActiveBranchCommitStatus> activeBranchCommitsStatus)
		{
			_upstreamStatusCache = upstreamStatusCache;
		}

		public UpstreamStatus? GetUpstreamStatus(LocalBranch localBranch)
		{
			if (_upstreamStatusCache.TryGetValue(localBranch.FullReference, out var value))
			{
				return value;
			}
			return null;
		}

		public bool DataEquals(UpstreamStatusCache other)
		{
			if (_upstreamStatusCache.Count != other._upstreamStatusCache.Count)
			{
				return false;
			}
			foreach (KeyValuePair<string, UpstreamStatus> item in _upstreamStatusCache)
			{
				if (other._upstreamStatusCache.TryGetValue(item.Key, out var value))
				{
					if (item.Value.Ahead != value.Ahead)
					{
						return false;
					}
					if (item.Value.Behind != value.Behind)
					{
						return false;
					}
				}
			}
			return true;
		}
	}
}
