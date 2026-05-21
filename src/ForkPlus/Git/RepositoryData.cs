using System;
using ForkPlus.UI.CustomCommands;

namespace ForkPlus.Git
{
	public class RepositoryData
	{
		public static readonly RepositoryData Empty = new RepositoryData(new GitConfig(new GitConfig.Section[0]), null, RepositoryReferences.Empty, RevisionStorage.Empty, RevisionSortOrder.Topo, reflog: false, CollapseState.Empty, UpstreamStatusCache.Empty, RepositoryRemotes.Empty, new RepositorySubmodules(new Submodule[0], null), RepositoryStashes.Empty, showStashesInRevisionList: true, new RepositoryWorktrees(null, new Worktree[0]), new BugtrackerLinkDefinition[0], new CustomCommand[0], null, UserColors.Empty, null, gitLfsInitialized: false);

		public GitConfig GitConfig { get; }

		public DateTime? GitConfigUpdateTime { get; }

		public RepositoryReferences References { get; }

		public RevisionStorage RevisionStorage { get; }

		public RevisionSortOrder SortOrder { get; }

		public bool Reflog { get; }

		public CollapseState CollapseState { get; }

		public UpstreamStatusCache UpstreamStatus { get; }

		public RepositoryRemotes Remotes { get; }

		public RepositorySubmodules Submodules { get; }

		public RepositoryStashes Stashes { get; }

		public bool ShowStashesInRevisionList { get; }

		public RepositoryWorktrees Worktrees { get; }

		public BugtrackerLinkDefinition[] Bugtrackers { get; }

		public CustomCommand[] CustomCommands { get; }

		public GitFlowSettings GitFlowSettings { get; }

		public UserColors UserColors { get; }

		public DateTime? GitLfsUpdateTime { get; }

		public bool GitLfsInitialized { get; }

		public RepositoryData(GitConfig gitConfig, DateTime? gitConfigUpdateTime, RepositoryReferences references, RevisionStorage revisionStorage, RevisionSortOrder sortOrder, bool reflog, CollapseState collapseState, UpstreamStatusCache upstreamStatus, RepositoryRemotes remotes, RepositorySubmodules submodules, RepositoryStashes stashes, bool showStashesInRevisionList, RepositoryWorktrees worktrees, BugtrackerLinkDefinition[] bugtrackers, CustomCommand[] customCommands, GitFlowSettings gitFlowSettings, UserColors userColors, DateTime? gitLfsUpdateTime, bool gitLfsInitialized)
		{
			GitConfig = gitConfig;
			GitConfigUpdateTime = gitConfigUpdateTime;
			References = references;
			RevisionStorage = revisionStorage;
			SortOrder = sortOrder;
			Reflog = reflog;
			CollapseState = collapseState;
			UpstreamStatus = upstreamStatus;
			Remotes = remotes;
			Submodules = submodules;
			Stashes = stashes;
			ShowStashesInRevisionList = showStashesInRevisionList;
			Worktrees = worktrees;
			Bugtrackers = bugtrackers;
			CustomCommands = customCommands;
			GitFlowSettings = gitFlowSettings;
			UserColors = userColors;
			GitLfsUpdateTime = gitLfsUpdateTime;
			GitLfsInitialized = gitLfsInitialized;
		}
	}
}
