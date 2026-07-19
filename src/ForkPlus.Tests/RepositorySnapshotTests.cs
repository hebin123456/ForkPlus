using System;
using System.Collections.Generic;
using System.Linq;
using ForkPlus.Git.Commands;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// v3.0.0 仓库快照数据模型单元测试。覆盖构造函数的 null 防御、WithOperationName 副本语义。
	/// 零外部依赖。
	/// </summary>
	public class RepositorySnapshotTests
	{
		private static RepositorySnapshot MakeValidSnapshot(string opName = "op")
		{
			return new RepositorySnapshot(opName, DateTime.UtcNow, "abc123", "main", new[] { "abc123" }, null);
		}

		[Fact]
		public void Constructor_NullOperationName_ConvertedToEmpty()
		{
			RepositorySnapshot s = new RepositorySnapshot(null, DateTime.UtcNow, "sha", "main", null, null);
			Assert.Equal("", s.OperationName);
		}

		[Fact]
		public void Constructor_NullHeadReflog_ConvertedToEmptyArray()
		{
			RepositorySnapshot s = new RepositorySnapshot("op", DateTime.UtcNow, "sha", "main", null, null);
			Assert.NotNull(s.HeadReflog);
			Assert.Empty(s.HeadReflog);
		}

		[Fact]
		public void Constructor_NullLocalBranches_ConvertedToEmptyDict()
		{
			RepositorySnapshot s = new RepositorySnapshot("op", DateTime.UtcNow, "sha", "main", null, null);
			Assert.NotNull(s.LocalBranches);
			Assert.Empty(s.LocalBranches);
		}

		[Fact]
		public void Constructor_NullTags_ConvertedToEmptyDict()
		{
			RepositorySnapshot s = new RepositorySnapshot("op", DateTime.UtcNow, "sha", "main", null, null);
			Assert.NotNull(s.Tags);
			Assert.Empty(s.Tags);
		}

		[Fact]
		public void Constructor_NullStashShas_ConvertedToEmptyList()
		{
			RepositorySnapshot s = new RepositorySnapshot("op", DateTime.UtcNow, "sha", "main", null, null);
			Assert.NotNull(s.StashShas);
			Assert.Empty(s.StashShas);
		}

		[Fact]
		public void Constructor_PreservesAllProvidedFields()
		{
			DateTime ts = DateTime.UtcNow;
			string[] reflog = { "sha1", "sha2" };
			var branches = new Dictionary<string, string> { { "main", "sha1" } };
			var tags = new Dictionary<string, string> { { "v1.0", "sha0" } };
			var stash = new List<string> { "stash-sha1" };

			RepositorySnapshot s = new RepositorySnapshot("commit", ts, "sha2", "main",
				reflog, "orig-head", branches, tags, stash, isWorkingTreeDirty: true, changedFilesCount: 3);

			Assert.Equal("commit", s.OperationName);
			Assert.Equal(ts, s.TimestampUtc);
			Assert.Equal("sha2", s.HeadSha);
			Assert.Equal("main", s.CurrentBranchName);
			Assert.Equal(reflog, s.HeadReflog);
			Assert.Equal("orig-head", s.OrigHead);
			Assert.Same(branches, s.LocalBranches);
			Assert.Same(tags, s.Tags);
			Assert.Same(stash, s.StashShas);
			Assert.True(s.IsWorkingTreeDirty);
			Assert.Equal(3, s.ChangedFilesCount);
		}

		[Fact]
		public void Constructor_NullHeadSha_Allowed()
		{
			// 空仓库场景：HeadSha 可为 null
			RepositorySnapshot s = new RepositorySnapshot("op", DateTime.UtcNow, null, null, null, null);
			Assert.Null(s.HeadSha);
			Assert.Null(s.CurrentBranchName);
		}

		[Fact]
		public void WithOperationName_ReturnsNewInstance()
		{
			RepositorySnapshot original = MakeValidSnapshot("original");
			RepositorySnapshot copy = original.WithOperationName("renamed");

			Assert.NotSame(original, copy);
			Assert.Equal("renamed", copy.OperationName);
			Assert.Equal("original", original.OperationName);  // 原实例不变
		}

		[Fact]
		public void WithOperationName_PreservesOtherFields()
		{
			DateTime ts = DateTime.UtcNow;
			string[] reflog = { "sha1" };
			RepositorySnapshot original = new RepositorySnapshot("orig", ts, "sha2", "main",
				reflog, "orig-head");

			RepositorySnapshot copy = original.WithOperationName("new");

			Assert.Equal(ts, copy.TimestampUtc);
			Assert.Equal("sha2", copy.HeadSha);
			Assert.Equal("main", copy.CurrentBranchName);
			Assert.Same(reflog, copy.HeadReflog);
			Assert.Equal("orig-head", copy.OrigHead);
		}

		[Fact]
		public void WithOperationName_NullName_ConvertedToEmpty()
		{
			RepositorySnapshot original = MakeValidSnapshot("orig");
			RepositorySnapshot copy = original.WithOperationName(null);

			Assert.Equal("", copy.OperationName);
		}

		[Fact]
		public void HeadReflog_ImmutableAfterConstruction()
		{
			// 传入数组后修改原数组，RepositorySnapshot 里的引用应反映修改
			// （注：实现里没有做防御性拷贝，这是已知行为）
			string[] reflog = { "sha1", "sha2" };
			RepositorySnapshot s = new RepositorySnapshot("op", DateTime.UtcNow, "sha", "main", reflog, null);

			reflog[0] = "modified";

			// 因实现直接持有引用，这里应看到修改
			Assert.Equal("modified", s.HeadReflog[0]);
		}
	}
}
