// v4.0.5（2026-09-09）：git 版本能力探测（GitCapabilities）单元测试。
// 背景：交互式变基 --update-refs（git 2.38+）与变基冲突预检 git replay（git 2.44+）
// 在主流发行版自带 git（Ubuntu 22.04=2.34 / 24.04=2.43）上不存在——此前硬编码导致
// 变基功能整体报错（E2e13 三个失败用例的根因），产品改为按能力降级。
using System;
using ForkPlus.Git.Commands;
using Xunit;

namespace ForkPlus.Tests
{
	public class GitCapabilitiesTests
	{
		[Fact]
		public void GetVersion_RealGitPath_ParsesAndCaches()
		{
			string gitPath = App.GitPath;
			Assert.False(string.IsNullOrWhiteSpace(gitPath), "测试环境应有可用 git（App.GitPath）");
			Version first = GitCapabilities.GetVersion(gitPath);
			Assert.NotNull(first);
			// git version 输出至少是 2.x 级别（MinimumRequiredVersion=2.31 起步的现行生态）
			Assert.True(first >= new Version(2, 0, 0), "解析出的版本应 ≥2.0: " + first);
			// 同路径缓存：两次探测返回同一实例（第二次不再 spawn git 进程）
			Version second = GitCapabilities.GetVersion(gitPath);
			Assert.Same(first, second);
		}

		[Fact]
		public void GetVersion_NullOrWhitespace_ReturnsNull()
		{
			Assert.Null(GitCapabilities.GetVersion(null));
			Assert.Null(GitCapabilities.GetVersion(""));
			Assert.Null(GitCapabilities.GetVersion("   "));
		}

		[Fact]
		public void GetVersion_NonexistentPath_ReturnsNull()
		{
			Assert.Null(GitCapabilities.GetVersion("/nonexistent/git-binary-should-not-exist"));
		}

		[Fact]
		public void CapabilityQueries_ConsistentWithDetectedVersion()
		{
			// 沙箱 git 2.34 两项能力均 false；CI 新版 git 两项均 true——断言与实际探测
			// 版本的阈值比较一致，环境无关（不写死 true/false 才能同时在这两类环境跑）。
			Version version = GitCapabilities.GetVersion();
			Assert.NotNull(version);
			Assert.Equal(version >= GitCapabilities.UpdateRefsMinVersion, GitCapabilities.SupportsUpdateRefs());
			Assert.Equal(version >= GitCapabilities.ReplayMinVersion, GitCapabilities.SupportsReplay());
		}

		[Fact]
		public void VersionThresholds_MatchGitReleaseHistory()
		{
			// 阈值即 git 官方引入版本：rebase --update-refs 于 2.38、replay 于 2.44。
			// 写死防止未来误调（2.38 前的 git 带 --update-refs 会被整条拒收）。
			Assert.Equal(new Version(2, 38, 0), GitCapabilities.UpdateRefsMinVersion);
			Assert.Equal(new Version(2, 44, 0), GitCapabilities.ReplayMinVersion);
		}
	}
}
