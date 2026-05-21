namespace ForkPlus.Git
{
	public class GitFlowSettings
	{
		public string MasterBranch { get; }

		public string DevelopBranch { get; }

		public string FeaturePrefix { get; }

		public string ReleasePrefix { get; }

		public string HotfixPrefix { get; }

		public string VersionTag { get; }

		public GitFlowSettings(string masterBranch, string developBranch, string featurePrefix, string releasePrefix, string hotfixPrefix, string versionTag)
		{
			MasterBranch = masterBranch;
			DevelopBranch = developBranch;
			FeaturePrefix = featurePrefix;
			ReleasePrefix = releasePrefix;
			HotfixPrefix = hotfixPrefix;
			VersionTag = versionTag;
		}
	}
}
