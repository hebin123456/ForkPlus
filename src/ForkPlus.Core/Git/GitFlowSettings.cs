using System;

namespace ForkPlus.Git
{
    public class GitFlowSettings
    {
        [Null]
        public string MasterBranchName { get; }

        [Null]
        public string DevelopBranchName { get; }

        [Null]
        public string FeatureBranchPrefix { get; }

        [Null]
        public string ReleaseBranchPrefix { get; }

        [Null]
        public string HotfixBranchPrefix { get; }

        [Null]
        public string SupportBranchPrefix { get; }

        [Null]
        public string VersionTagPrefix { get; }

        public GitFlowSettings([Null] string masterBranchName, [Null] string developBranchName, [Null] string featureBranchPrefix, [Null] string releaseBranchPrefix, [Null] string hotfixBranchPrefix, [Null] string supportBranchPrefix, [Null] string versionTagPrefix)
        {
            MasterBranchName = masterBranchName;
            DevelopBranchName = developBranchName;
            FeatureBranchPrefix = featureBranchPrefix;
            ReleaseBranchPrefix = releaseBranchPrefix;
            HotfixBranchPrefix = hotfixBranchPrefix;
            SupportBranchPrefix = supportBranchPrefix;
            VersionTagPrefix = versionTagPrefix;
        }
    }
}
