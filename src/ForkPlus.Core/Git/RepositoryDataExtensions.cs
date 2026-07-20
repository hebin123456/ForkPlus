namespace ForkPlus.Git
{
	public static class RepositoryDataExtensions
	{
		public static RepositoryData With(this RepositoryData this_, CollapseState collapseState)
		{
			return new RepositoryData(this_.GitConfig, this_.GitConfigUpdateTime, this_.References, this_.RevisionStorage, this_.SortOrder, this_.Reflog, collapseState, this_.UpstreamStatus, this_.Remotes, this_.Submodules, this_.Stashes, this_.ShowStashesInRevisionList, this_.Worktrees, this_.Bugtrackers, this_.CustomCommands, this_.GitFlowSettings, this_.UserColors, this_.GitLfsUpdateTime, this_.GitLfsInitialized);
		}

		public static RepositoryData With(this RepositoryData this_, RevisionStorage revisionStorage)
		{
			return new RepositoryData(this_.GitConfig, this_.GitConfigUpdateTime, this_.References, revisionStorage, this_.SortOrder, this_.Reflog, this_.CollapseState, this_.UpstreamStatus, this_.Remotes, this_.Submodules, this_.Stashes, this_.ShowStashesInRevisionList, this_.Worktrees, this_.Bugtrackers, this_.CustomCommands, this_.GitFlowSettings, this_.UserColors, this_.GitLfsUpdateTime, this_.GitLfsInitialized);
		}
	}
}
