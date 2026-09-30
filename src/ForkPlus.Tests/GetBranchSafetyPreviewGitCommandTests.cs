// WS2.1 删除本地分支安全预览：三个 Git 命令的非 UI 单测。
// 直接对 TestRepoFactory 的真实临时仓库跑 for-each-ref / rev-list 断言。
// 注：断言用关系式（≥）而非精确值——本机后台服务可能向临时目录的仓库追加探测提交
// （author=git-ai），精确计数会被环境噪声打挂；结构性断言（包含/不包含）不受影响。
using System;
using System.IO;
using System.Linq;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using Xunit;

namespace ForkPlus.Tests
{
	public class GetBranchSafetyPreviewGitCommandTests
	{
		private static GitModule ModuleOf(string repoRoot)
		{
			return new GitModule(repoRoot, Path.Combine(repoRoot, ".git"), null, null);
		}

		[Fact]
		public void MergedBranches_MainBase_ContainsMainButNotFeatureBranches()
		{
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				GitModule module = ModuleOf(repo);
				GitCommandResult<string[]> result = new GetMergedBranchesGitCommand().Execute(module, "main");
				Assert.True(result.Succeeded, "for-each-ref --merged=main 应成功");
				// main 合并进自身 → 列表含 base 自身（调用方负责排除）
				Assert.Contains("refs/heads/main", result.Result);
				// feature 分支各有独有提交，未合并进 main
				Assert.DoesNotContain("refs/heads/feature/one", result.Result);
				Assert.DoesNotContain("refs/heads/feature/two", result.Result);
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void MergedBranches_NullOrEmptyBase_FallsBackToHead()
		{
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				GitModule module = ModuleOf(repo);
				GitCommandResult<string[]> withNull = new GetMergedBranchesGitCommand().Execute(module, null);
				GitCommandResult<string[]> withEmpty = new GetMergedBranchesGitCommand().Execute(module, "");
				Assert.True(withNull.Succeeded, "base=null 应回退 HEAD 并成功");
				Assert.True(withEmpty.Succeeded, "base=空串应回退 HEAD 并成功");
				// HEAD = main（仓库工厂收尾 checkout main）
				Assert.Contains("refs/heads/main", withNull.Result);
				Assert.Contains("refs/heads/main", withEmpty.Result);
				Assert.DoesNotContain("refs/heads/feature/one", withNull.Result);
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void MergedBranches_FeatureBase_IncludesBaseItself()
		{
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				GitModule module = ModuleOf(repo);
				GitCommandResult<string[]> result = new GetMergedBranchesGitCommand().Execute(module, "refs/heads/feature/one");
				Assert.True(result.Succeeded, "base 支持任意可解析 ref");
				// feature/one 合并进自身；main（领先）未合并进 feature/one
				Assert.Contains("refs/heads/feature/one", result.Result);
				Assert.DoesNotContain("refs/heads/main", result.Result);
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void UniqueCommits_CountsCommitsOnlyOnThatBranch()
		{
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				GitModule module = ModuleOf(repo);
				// c4 只在 feature/one（c1 被其他分支共享，c2/c3 在 main，c5 在 feature/two）
				GitCommandResult<int> featureOne = new GetBranchUniqueCommitsGitCommand().Execute(module, "refs/heads/feature/one");
				Assert.True(featureOne.Succeeded, "rev-list --count 应成功");
				Assert.True(featureOne.Result >= 1, "feature/one 至少 1 个独有提交（c4），实际 " + featureOne.Result);

				GitCommandResult<int> featureTwo = new GetBranchUniqueCommitsGitCommand().Execute(module, "refs/heads/feature/two");
				Assert.True(featureTwo.Succeeded, "rev-list --count 应成功");
				Assert.True(featureTwo.Result >= 1, "feature/two 至少 1 个独有提交（c5），实际 " + featureTwo.Result);

				// c2/c3 只在 main
				GitCommandResult<int> main = new GetBranchUniqueCommitsGitCommand().Execute(module, "refs/heads/main");
				Assert.True(main.Succeeded, "rev-list --count 应成功");
				Assert.True(main.Result >= 2, "main 至少 2 个独有提交（c2/c3），实际 " + main.Result);
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void UniqueCommits_ExcludeMustUseShortBranchName()
		{
			// 回归防线：--exclude 传 full ref 时 git 按 refs/heads/refs/heads/... 匹配，
			// 恒不命中 → 分支自身未被排除 → 计数恒 0（探针实证）。命令内部必须剥前缀。
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				GitModule module = ModuleOf(repo);
				string wrongCount = TestRepoFactory.GitOutput(repo, "rev-list --count refs/heads/feature/one --not --exclude=refs/heads/feature/one --branches --remotes");
				Assert.True(wrongCount.Trim() == "0", "前置校验：full ref 作 --exclude 不生效（计数恒 0），实际 " + wrongCount.Trim());
				GitCommandResult<int> result = new GetBranchUniqueCommitsGitCommand().Execute(module, "refs/heads/feature/one");
				Assert.True(result.Succeeded && result.Result >= 1, "命令内部必须用短名作 --exclude，实际 " + (result.Succeeded ? result.Result.ToString() : "失败"));
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void UnpushedCommits_NoUpstream_CountsAgainstAllRemotes()
		{
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				GitModule module = ModuleOf(repo);
				// 仓库无任何 remote：--not --remotes 不排除任何提交 → 全部计入
				GitCommandResult<int> featureOne = new GetBranchUnpushedCommitsGitCommand().Execute(module, "refs/heads/feature/one", null);
				Assert.True(featureOne.Succeeded, "upstream 为空走 --not --remotes 路径");
				Assert.True(featureOne.Result >= 2, "feature/one 至少计入 c1+c4 两个提交，实际 " + featureOne.Result);
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void UnpushedCommits_WithUpstream_CountsUpstreamRange()
		{
			// CreateRemoteBranches：main 与 origin/main 对齐（c1 已推）；
			// 测试内补一个 c2 使 main 领先 1；feature/one 仅本地（c1 已在 origin/main 上）
			string repo = TestRepoFactory.CreateRemoteBranches();
			try
			{
				TestRepoFactory.GitOutput(repo, "commit -q --allow-empty -m c2");
				GitModule module = ModuleOf(repo);
				GitCommandResult<int> main = new GetBranchUnpushedCommitsGitCommand().Execute(module, "refs/heads/main", "refs/remotes/origin/main");
				Assert.True(main.Succeeded, "upstream 非空走 upstream..branch 路径");
				Assert.True(main.Result >= 1, "main 相对 origin/main 至少 1 个未推送提交（c2 ahead），实际 " + main.Result);

				GitCommandResult<int> featureOne = new GetBranchUnpushedCommitsGitCommand().Execute(module, "refs/heads/feature/one", null);
				Assert.True(featureOne.Succeeded, "feature/one 无 upstream，走 --not --remotes 路径");
				Assert.Equal(0, featureOne.Result);
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}
	}
}
