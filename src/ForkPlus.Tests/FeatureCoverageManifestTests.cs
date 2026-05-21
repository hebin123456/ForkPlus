using System;
using System.Linq;
using Xunit;

namespace ForkPlus.Tests
{
	public class FeatureCoverageManifestTests
	{
		[Fact]
		public void EveryFeature_HasAtLeastOneAutomatedCase()
		{
			FeatureCoverageEntry[] missing = FeatureCoverageManifest.Entries
				.Where((FeatureCoverageEntry entry) => entry.AutomatedCases == null || entry.AutomatedCases.Length == 0)
				.ToArray();

			Assert.True(missing.Length == 0, "Features without automated cases:\n" + string.Join("\n", missing.Select((FeatureCoverageEntry entry) => entry.FeatureId)));
		}

		[Fact]
		public void FeatureIds_AreUnique()
		{
			string[] duplicates = FeatureCoverageManifest.Entries
				.GroupBy((FeatureCoverageEntry entry) => entry.FeatureId, StringComparer.OrdinalIgnoreCase)
				.Where((IGrouping<string, FeatureCoverageEntry> group) => group.Count() > 1)
				.Select((IGrouping<string, FeatureCoverageEntry> group) => group.Key)
				.ToArray();

			Assert.True(duplicates.Length == 0, "Duplicate feature ids:\n" + string.Join("\n", duplicates));
		}

		[Theory]
		[InlineData("git-mm.start")]
		[InlineData("git-mm.sync")]
		[InlineData("git-mm.upload")]
		[InlineData("commit.stage-unstage")]
		[InlineData("repository.status-refresh")]
		[InlineData("helpers.askpass")]
		[InlineData("helpers.ri")]
		public void CriticalFeatures_AreRegistered(string featureId)
		{
			Assert.Contains(FeatureCoverageManifest.Entries, (FeatureCoverageEntry entry) => string.Equals(entry.FeatureId, featureId, StringComparison.OrdinalIgnoreCase));
		}
	}
}
