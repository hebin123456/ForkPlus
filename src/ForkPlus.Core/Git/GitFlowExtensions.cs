namespace ForkPlus.Git
{
	internal static class GitFlowExtensions
	{
		public static bool IsGitFlowBranch(this LocalBranch localBranch, GitFlowSettings gitFlowSettings)
		{
			if (!localBranch.IsFeatureBranch(gitFlowSettings) && !localBranch.IsReleaseBranch(gitFlowSettings))
			{
				return localBranch.IsHotfixBranch(gitFlowSettings);
			}
			return true;
		}

		public static bool IsFeatureBranch(this LocalBranch localBranch, GitFlowSettings gitFlowSettings)
		{
			return localBranch.Name.StartsWith(gitFlowSettings.FeaturePrefix);
		}

		public static bool IsReleaseBranch(this LocalBranch localBranch, GitFlowSettings gitFlowSettings)
		{
			return localBranch.Name.StartsWith(gitFlowSettings.ReleasePrefix);
		}

		public static bool IsHotfixBranch(this LocalBranch localBranch, GitFlowSettings gitFlowSettings)
		{
			return localBranch.Name.StartsWith(gitFlowSettings.HotfixPrefix);
		}
	}
}
