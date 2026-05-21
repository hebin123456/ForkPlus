namespace ForkPlus.Git
{
	public class SubmoduleDiffContent : DiffContent
	{
		public Submodule Submodule { get; }

		public GitModule GitModule { get; }

		public GitModule ParentGitModule { get; }

		public GitConfig GitConfig { get; }

		[Null]
		public Revision SrcRevision { get; }

		[Null]
		public Revision DstRevision { get; }

		public Sha SrcSha { get; }

		public Sha DstSha { get; }

		public RepositoryReferences References { get; }

		public RepositoryRemotes Remotes { get; }

		public RevisionStorage RevisionStorage { get; }

		public BehindAheadCount BehindAheadCount { get; }

		public string[] ChangedFilePaths { get; }

		public BugtrackerLinkDefinition[] Bugtrackers { get; }

		public SubmoduleDiffContent(SubmoduleChangedFile changedFile, Submodule submodule, GitModule gitModule, GitConfig gitConfig, GitModule parentGitModule, [Null] Revision srcRevision, [Null] Revision dstRevision, Sha srcSha, Sha dstSha, RepositoryReferences references, RepositoryRemotes remotes, RevisionStorage revisionStorage, BehindAheadCount behindAheadCount, string[] changedFilePaths, BugtrackerLinkDefinition[] bugtrackers)
			: base(changedFile)
		{
			Submodule = submodule;
			GitModule = gitModule;
			ParentGitModule = parentGitModule;
			GitConfig = gitConfig;
			SrcSha = srcSha;
			DstSha = dstSha;
			SrcRevision = srcRevision;
			DstRevision = dstRevision;
			References = references;
			Remotes = remotes;
			RevisionStorage = revisionStorage;
			BehindAheadCount = behindAheadCount;
			ChangedFilePaths = changedFilePaths;
			Bugtrackers = bugtrackers;
		}
	}
}
