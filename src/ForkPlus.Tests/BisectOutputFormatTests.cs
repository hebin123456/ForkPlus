// 回归测试（2026-09-12，CI run 34678720133 失败根因——git 2.55 bisect 输出格式变更）：
// git 2.55.0（2026 起，CI ubuntu-latest 已预装；本地沙箱仍 2.34.1）把 bisect 收敛输出
// 的术语加了单引号（builtin/bisect.c 的 printf "%s is the first '%s' commit"、
// BISECT_LOG 的 "# first '%s' commit: [...]"，≤2.54 无引号）。此前生产与测试各自
// 硬编码旧格式 "is the first bad commit" / "# first bad commit:"：
//   ① 生产 bug：git 2.55 上 BisectGitCommand 检测不到收敛 → 收敛被当普通成功返回，
//      "找到首个坏提交"的信息窗不弹（BisectCommand 分流失效）、活动日志不着色
//      （GitOutputColorizer）；
//   ② CI 失败：bisect E2E 的收敛轮询（HasConverged/WaitForAdvanceOrConverge）永远
//      等不到旧格式结论行 → 30s 超时 → "二分应收敛"断言失败（本地 2.34.1 从不复现）。
// 修复：两代格式判定集中到 BisectGitCommand（IsFirstBadCommitConclusion /
// LogHasFirstBadCommitConclusion / LogConcludesFirstBadCommitIs），生产 3 处 + E2E
// 测试 4 处共用。本用例锁定该口径：两代格式的正/负样本 + 边界（null/空串）。
// 纯字符串单元级，无 git、无 headless UI——CI 慢机也毫秒级稳定。
using ForkPlus.Git.Commands;
using Xunit;

namespace ForkPlus.Tests
{
	public class BisectOutputFormatTests
	{
		private const string Sha1 = "3d1f0a2b9c8e7f6a5b4c3d2e1f0a9b8c7d6e5f4a";
		private const string Sha2 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

		// ===== IsFirstBadCommitConclusion：stdout 收敛结论（BisectGitCommand.Execute 用）=====

		[Theory]
		[InlineData("3d1f0a2b9c8e7f6a5b4c3d2e1f0a9b8c7d6e5f4a is the first bad commit")]        // git ≤2.54
		[InlineData("3d1f0a2b9c8e7f6a5b4c3d2e1f0a9b8c7d6e5f4a is the first 'bad' commit")]      // git ≥2.55
		[InlineData("stdout 混合输出\n3d1f0a2b9c8e7f6a5b4c3d2e1f0a9b8c7d6e5f4a is the first 'bad' commit\n")] // 混在多行输出里
		public void IsFirstBadCommitConclusion_MatchesBothGitFormats(string output)
		{
			Assert.True(BisectGitCommand.IsFirstBadCommitConclusion(output));
		}

		[Theory]
		[InlineData("Bisecting: 2 revisions left to test after this (roughly 1 step)")] // 推进输出不是收敛
		[InlineData("You need to start by \"git bisect start\"")]                       // 错误输出
		[InlineData("is the first good commit")]                                        // 术语不同（good≠bad）
		[InlineData("is the first bad")]                                                // 截断文案
		[InlineData("")]
		[InlineData(null)]
		public void IsFirstBadCommitConclusion_RejectsNonConvergence(string output)
		{
			Assert.False(BisectGitCommand.IsFirstBadCommitConclusion(output));
		}

		// ===== LogHasFirstBadCommitConclusion：BISECT_LOG 结论行（E2E 收敛轮询用）=====

		[Theory]
		[InlineData("# first bad commit: [3d1f0a2b9c8e7f6a5b4c3d2e1f0a9b8c7d6e5f4a] c5\n")]   // git ≤2.54
		[InlineData("# first 'bad' commit: [3d1f0a2b9c8e7f6a5b4c3d2e1f0a9b8c7d6e5f4a] c5\n")] // git ≥2.55
		[InlineData("git bisect start 'HEAD'\n# bad: [aaaaaaaa] c8\n# first 'bad' commit: [3d1f0a2b] c5\n")]
		public void LogHasFirstBadCommitConclusion_MatchesBothGitFormats(string logText)
		{
			Assert.True(BisectGitCommand.LogHasFirstBadCommitConclusion(logText));
		}

		[Theory]
		[InlineData("# good: [aaaaaaaa] c1\n")]                       // 只有标记行
		[InlineData("# bad: [aaaaaaaa] c8\n")]
		[InlineData("# first good commit: [aaaaaaaa] c1\n")]          // 术语不同
		[InlineData("# possible first 'bad' commit: [a] c\n")]        // "possible first"≠已收敛
		[InlineData("")]
		[InlineData(null)]
		public void LogHasFirstBadCommitConclusion_RejectsNonConvergence(string logText)
		{
			Assert.False(BisectGitCommand.LogHasFirstBadCommitConclusion(logText));
		}

		// ===== LogConcludesFirstBadCommitIs：结论行指向指定提交（E2E 收敛 SHA 断言用）=====

		[Theory]
		[InlineData("# first bad commit: [3d1f0a2b9c8e7f6a5b4c3d2e1f0a9b8c7d6e5f4a] c5\n")]   // git ≤2.54
		[InlineData("# first 'bad' commit: [3d1f0a2b9c8e7f6a5b4c3d2e1f0a9b8c7d6e5f4a] c5\n")] // git ≥2.55
		public void LogConcludesFirstBadCommitIs_MatchesExpectedShaInBothFormats(string logText)
		{
			Assert.True(BisectGitCommand.LogConcludesFirstBadCommitIs(logText, Sha1));
		}

		[Theory]
		[InlineData("# first bad commit: [3d1f0a2b9c8e7f6a5b4c3d2e1f0a9b8c7d6e5f4a] c5\n")]   // 收敛但指向别的提交
		[InlineData("# first 'bad' commit: [3d1f0a2b9c8e7f6a5b4c3d2e1f0a9b8c7d6e5f4a] c5\n")]
		[InlineData("# good: [aaaaaaaa] c1\n")]                        // 无结论行
		public void LogConcludesFirstBadCommitIs_RejectsOtherSha(string logText)
		{
			Assert.False(BisectGitCommand.LogConcludesFirstBadCommitIs(logText, Sha2));
		}

		[Fact]
		public void LogConcludesFirstBadCommitIs_NullOrEmptyInputs_ReturnFalse()
		{
			Assert.False(BisectGitCommand.LogConcludesFirstBadCommitIs(null, Sha1));
			Assert.False(BisectGitCommand.LogConcludesFirstBadCommitIs("", Sha1));
			Assert.False(BisectGitCommand.LogConcludesFirstBadCommitIs("# first bad commit: [" + Sha1 + "] c5\n", null));
			Assert.False(BisectGitCommand.LogConcludesFirstBadCommitIs("# first bad commit: [" + Sha1 + "] c5\n", ""));
		}
	}
}
