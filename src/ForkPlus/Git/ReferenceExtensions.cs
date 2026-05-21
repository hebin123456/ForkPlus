using ForkPlus.Biturbo;
using ForkPlus.Git.Commands;

namespace ForkPlus.Git
{
	public static class ReferenceExtensions
	{
		public static bool IsInfrontUpstream(this LocalBranch localBranch, RemoteBranch upstream, GitModule gitModule, CommitGraphCache commitGraphCache)
		{
			GitCommandResult<BehindAheadCount> gitCommandResult = new GetBehindAheadCountGitCommand().Execute(gitModule, localBranch.Sha, upstream.Sha, commitGraphCache);
			if (!gitCommandResult.Succeeded)
			{
				Log.Warn(gitCommandResult.Error.FriendlyDescription);
				return false;
			}
			BehindAheadCount result = gitCommandResult.Result;
			if (result.Left > 0)
			{
				if (result.Right > 0)
				{
					return false;
				}
				return true;
			}
			return false;
		}

		public static bool IsAhead(this Branch branch, Branch otherBranch, GitModule gitModule, CommitGraphCache commitGraphCache)
		{
			GitCommandResult<BehindAheadCount> gitCommandResult = new GetBehindAheadCountGitCommand().Execute(gitModule, branch.Sha, otherBranch.Sha, commitGraphCache);
			if (!gitCommandResult.Succeeded)
			{
				Log.Warn(gitCommandResult.Error.FriendlyDescription);
				return false;
			}
			return gitCommandResult.Result.Left > 0;
		}

		public static string LastNameComponent(this LocalBranch localBranch)
		{
			string[] array = localBranch.Name.Split('/');
			int num = array.Length - 1;
			return array[num];
		}
	}
}
