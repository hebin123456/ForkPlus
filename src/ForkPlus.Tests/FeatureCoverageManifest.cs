using System.Collections.Generic;

namespace ForkPlus.Tests
{
	internal sealed class FeatureCoverageEntry
	{
		public string FeatureId { get; }

		public string Area { get; }

		public string[] AutomatedCases { get; }

		public FeatureCoverageEntry(string featureId, string area, params string[] automatedCases)
		{
			FeatureId = featureId;
			Area = area;
			AutomatedCases = automatedCases;
		}
	}

	internal static class FeatureCoverageManifest
	{
		public static readonly IReadOnlyList<FeatureCoverageEntry> Entries = new[]
		{
			new FeatureCoverageEntry("app.startup", "Application", "AUTO-SMOKE-001"),
			new FeatureCoverageEntry("app.theme-and-localization", "Application", "UNIT-LOCALIZATION-001"),
			new FeatureCoverageEntry("repository.manager", "Repository Manager", "UNIT-SOURCE-COVERAGE-001"),
			new FeatureCoverageEntry("repository.open", "Repository", "AUTO-SMOKE-001"),
			new FeatureCoverageEntry("repository.status-refresh", "Repository", "UNIT-GIT-STATUS-001", "UNIT-PERF-TELEMETRY-001"),
			new FeatureCoverageEntry("repository.changed-files", "Repository", "UNIT-GIT-STATUS-002", "UNIT-STATUS-NORMALIZER-001"),
			new FeatureCoverageEntry("repository.file-list", "Commit", "UNIT-SOURCE-COVERAGE-001"),
			new FeatureCoverageEntry("commit.stage-unstage", "Commit", "UNIT-SOURCE-COVERAGE-001"),
			new FeatureCoverageEntry("commit.commit-message", "Commit", "UNIT-RANGE-001"),
			new FeatureCoverageEntry("commit.amend", "Commit", "UNIT-SOURCE-COVERAGE-001"),
			new FeatureCoverageEntry("diff.text", "Diff", "UNIT-RANGE-001"),
			new FeatureCoverageEntry("diff.binary", "Diff", "UNIT-SOURCE-COVERAGE-001"),
			new FeatureCoverageEntry("diff.popup", "Diff", "UNIT-LOCALIZATION-001"),
			new FeatureCoverageEntry("history.revision-list", "History", "UNIT-SOURCE-COVERAGE-001"),
			new FeatureCoverageEntry("history.file-history", "History", "UNIT-SOURCE-COVERAGE-001"),
			new FeatureCoverageEntry("history.blame", "History", "UNIT-SOURCE-COVERAGE-001"),
			new FeatureCoverageEntry("branch.checkout", "Branches", "UNIT-REFERENCE-NAME-001"),
			new FeatureCoverageEntry("branch.create-rename-delete", "Branches", "UNIT-REFERENCE-NAME-001"),
			new FeatureCoverageEntry("branch.merge-rebase", "Branches", "UNIT-SOURCE-COVERAGE-001"),
			new FeatureCoverageEntry("interactive-rebase", "Rebase", "AUTO-RI-001"),
			new FeatureCoverageEntry("stash.apply-save-delete", "Stash", "UNIT-SOURCE-COVERAGE-001"),
			new FeatureCoverageEntry("tags.create-push-delete", "Tags", "UNIT-REFERENCE-NAME-001"),
			new FeatureCoverageEntry("remotes.fetch-pull-push", "Remotes", "UNIT-GIT-COMMAND-001"),
			new FeatureCoverageEntry("submodules.open-update", "Submodules", "UNIT-GIT-STATUS-001"),
			new FeatureCoverageEntry("worktrees.open-create-delete", "Worktrees", "UNIT-PATH-001"),
			new FeatureCoverageEntry("git-lfs.fetch-pull-lock", "Git LFS", "UNIT-GIT-COMMAND-001"),
			new FeatureCoverageEntry("git-flow.start-finish", "Git Flow", "UNIT-REFERENCE-NAME-001"),
			new FeatureCoverageEntry("git-mm.workspace-scan", "git mm", "UNIT-SOURCE-COVERAGE-001", "UNIT-PERF-TELEMETRY-001"),
			new FeatureCoverageEntry("git-mm.start", "git mm", "UNIT-GIT-COMMAND-001"),
			new FeatureCoverageEntry("git-mm.sync", "git mm", "UNIT-GIT-COMMAND-001"),
			new FeatureCoverageEntry("git-mm.upload", "git mm", "UNIT-GIT-COMMAND-001"),
			new FeatureCoverageEntry("git-mm.reference-manual", "git mm", "UNIT-GITMM-MANUAL-001"),
			new FeatureCoverageEntry("git-mm.link-filtering", "git mm", "UNIT-STATUS-NORMALIZER-001"),
			new FeatureCoverageEntry("accounts.oauth-login", "Accounts", "UNIT-SOURCE-COVERAGE-001"),
			new FeatureCoverageEntry("accounts.notifications", "Accounts", "UNIT-SOURCE-COVERAGE-001"),
			new FeatureCoverageEntry("ai.code-review", "AI", "UNIT-GITMM-MANUAL-001"),
			new FeatureCoverageEntry("preferences.general", "Preferences", "UNIT-LOCALIZATION-001"),
			new FeatureCoverageEntry("preferences.git-instance", "Preferences", "UNIT-PATH-001"),
			new FeatureCoverageEntry("preferences.custom-commands", "Preferences", "UNIT-SOURCE-COVERAGE-001"),
			new FeatureCoverageEntry("helpers.askpass", "Helpers", "UNIT-ASKPASS-001"),
			new FeatureCoverageEntry("helpers.ri", "Helpers", "AUTO-RI-001"),
			new FeatureCoverageEntry("ipc.pipe-protocol", "IPC", "UNIT-IPC-001"),
			new FeatureCoverageEntry("performance.observability", "Infrastructure", "UNIT-PERF-TELEMETRY-001"),
			new FeatureCoverageEntry("logging.configuration", "Infrastructure", "UNIT-ASSEMBLY-SMOKE-001"),
			new FeatureCoverageEntry("settings.persistence", "Infrastructure", "UNIT-SOURCE-COVERAGE-001")
		};
	}
}
