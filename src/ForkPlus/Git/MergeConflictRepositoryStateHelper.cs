namespace ForkPlus.Git
{
	public static class MergeConflictRepositoryStateHelper
	{
		public static readonly string Stash = "stash/patch";

		public static IGitPoint GetRemoteGitPoint([Null] RepositoryState repositoryState)
		{
			if (repositoryState is RepositoryState.MergeInProgress mergeInProgress)
			{
				return mergeInProgress.Remote;
			}
			if (repositoryState is RepositoryState.RebaseInProgress rebaseInProgress)
			{
				return rebaseInProgress.Remote;
			}
			if (repositoryState is RepositoryState.CherryPickInProgress cherryPickInProgress)
			{
				return cherryPickInProgress.CherryPickHead;
			}
			if (repositoryState is RepositoryState.SquashInProgress)
			{
				return new SymbolicReference("", "remote");
			}
			if (repositoryState is RepositoryState.AmInProgress)
			{
				return new SymbolicReference("", "patch");
			}
			if (repositoryState is RepositoryState.UnmergedIndex)
			{
				return new SymbolicReference("", Stash);
			}
			return new SymbolicReference("", "remote");
		}

		public static IGitPoint GetLocalGitPoint([Null] RepositoryState repositoryState)
		{
			if (repositoryState is RepositoryState.MergeInProgress mergeInProgress)
			{
				return mergeInProgress.Local;
			}
			if (repositoryState is RepositoryState.RebaseInProgress rebaseInProgress)
			{
				return rebaseInProgress.Local;
			}
			if (repositoryState is RepositoryState.CherryPickInProgress cherryPickInProgress)
			{
				return cherryPickInProgress.Head;
			}
			if (repositoryState is RepositoryState.SquashInProgress squashInProgress)
			{
				return squashInProgress.Head;
			}
			if (repositoryState is RepositoryState.AmInProgress amInProgress)
			{
				return amInProgress.Local;
			}
			if (repositoryState is RepositoryState.UnmergedIndex unmergedIndex)
			{
				return unmergedIndex.Head;
			}
			return new SymbolicReference("", "local");
		}
	}
}
