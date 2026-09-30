// WS4 仓库健康仪表盘：GetBiggestBlobsGitCommand 非 UI 单测。
// 对 TestRepoFactory 的真实临时仓库跑 cat-file --batch-all-objects / rev-list 断言。
// 注：断言用关系式（≥/≤）与结构性包含而非精确名次——本机后台服务可能向临时目录的仓库
// 追加探测提交（author=git-ai），精确排序断言会被环境噪声打挂（口径同
// GetBranchSafetyPreviewGitCommandTests）。
using System;
using System.IO;
using System.Linq;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using Xunit;

namespace ForkPlus.Tests
{
	public class GetBiggestBlobsGitCommandTests
	{
		private static GitModule ModuleOf(string repoRoot)
		{
			return new GitModule(repoRoot, Path.Combine(repoRoot, ".git"), null, null);
		}

		[Fact]
		public void LargestBlobs_BasicRepo_SortedDescAndContainsCommittedFiles()
		{
			string repo = TestRepoFactory.CreateBasic();
			try
			{
				GitModule module = ModuleOf(repo);
				GitCommandResult<(string Path, long Size)[]> result = new GetBiggestBlobsGitCommand().Execute(module);
				Assert.True(result.Succeeded, "cat-file --batch-all-objects --batch-check 应成功");
				(string Path, long Size)[] blobs = result.Result;
				Assert.True(blobs.Length > 0, "基础仓库至少有 4 个文件 blob");
				Assert.True(blobs.Length <= GetBiggestBlobsGitCommand.DefaultMaxCount, "结果条数不超过 maxCount");
				// 体积降序
				for (int i = 1; i < blobs.Length; i++)
				{
					Assert.True(blobs[i - 1].Size >= blobs[i].Size, "结果应按体积降序排列");
				}
				// 工厂提交的 4 个文本文件 blob 均应出现，且带真实路径（rev-list 解析成功，非 sha 回退）
				Assert.Contains(blobs, ((string Path, long Size) b) => b.Path == "a.txt");
				Assert.Contains(blobs, ((string Path, long Size) b) => b.Path == "src/app.cs");
				Assert.Contains(blobs, ((string Path, long Size) b) => b.Path == "b.txt");
				Assert.Contains(blobs, ((string Path, long Size) b) => b.Path == "readme.md");
				// a.txt（HEAD 版本）体积与 cat-file -s 交叉验证
				string aTxtSha = TestRepoFactory.GitOutput(repo, "rev-parse HEAD:a.txt").Trim();
				string aTxtSize = TestRepoFactory.GitOutput(repo, "cat-file -s " + aTxtSha).Trim();
				(string Path, long Size) aTxtEntry = blobs.First(((string Path, long Size) b) => b.Path == "a.txt");
				Assert.Equal(long.Parse(aTxtSize), aTxtEntry.Size);
				// 所有条目体积为正
				Assert.All(blobs, ((string Path, long Size) b) => Assert.True(b.Size > 0));
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void LargestBlobs_SmallMaxCount_TruncatesResult()
		{
			string repo = TestRepoFactory.CreateBasic();
			try
			{
				GitModule module = ModuleOf(repo);
				GitCommandResult<(string Path, long Size)[]> result = new GetBiggestBlobsGitCommand().Execute(module, 2);
				Assert.True(result.Succeeded);
				Assert.True(result.Result.Length >= 1 && result.Result.Length <= 2, "maxCount=2 应把结果截断到 ≤2 条");
				for (int i = 1; i < result.Result.Length; i++)
				{
					Assert.True(result.Result[i - 1].Size >= result.Result[i].Size, "截断后仍应降序");
				}
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void LargestBlobs_NonPositiveMaxCount_FallsBackToDefault()
		{
			string repo = TestRepoFactory.CreateBasic();
			try
			{
				GitModule module = ModuleOf(repo);
				GitCommandResult<(string Path, long Size)[]> result = new GetBiggestBlobsGitCommand().Execute(module, 0);
				Assert.True(result.Succeeded, "maxCount=0 应回退 DefaultMaxCount 而非返回空");
				Assert.NotEmpty(result.Result);
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void LargestBlobs_EmptyRepo_SucceedsWithNoBlobs()
		{
			string repo = TestRepoFactory.CreateEmpty();
			try
			{
				GitModule module = ModuleOf(repo);
				GitCommandResult<(string Path, long Size)[]> result = new GetBiggestBlobsGitCommand().Execute(module);
				Assert.True(result.Succeeded, "空仓库（零对象零引用）应成功");
				Assert.Empty(result.Result);
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}
	}
}
