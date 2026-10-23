// 回归（2026-10，"轨道图好卡"审计）：worktree 引用刷新快路径 + headers 页加载守卫。
//
// 根因：RefreshRepositoryReferencesGitCommand 曾对 worktree 一刀切禁用 RefsHash 相等
// 快路径（bt 的 hash 不含链接 worktree 自身 HEAD，直接复用旧引用会让 ActiveBranch 停留
// 旧值）。代价：worktree 每次刷新都新建 ReferenceStorage → 下游"数据未变"短路永不命中
// → 每次刷新全量重建轨道图（用户日志：worktree 仓库 3 分钟 293 次全量刷新，普通仓库
// 同期仅 2-5 次；风暴期每次还伴随 5-6 次 settings 写盘与 git show 取消重启）。
//
// 修复：把本 worktree 的 HEAD 文件原文（ReferenceStorage.HeadSnapshot）纳入相等比较键
// ——HEAD 未变且 hash/上游未变 → 复用旧引用实例；HEAD 变了 → 重建并按新 HEAD 修正
// ActiveBranch（保留 v2.x "worktree 误显示 detached HEAD" 修复的语义）。
//
// 本文件用 TestRepoFactory.CreateWithWorktree 的真实 worktree 仓库走完整
// RefreshRepositoryReferencesGitCommand.Execute（真实 bt native 调用），验证：
//   ① worktree HEAD 未变 → 第二次刷新返回同一引用实例（快路径生效，修复前必为不同实例）；
//   ② worktree 切分支 → 重建 + ActiveBranch 指向新分支（历史 bug 不复活）+ 快路径重新挂上；
//   ③ detached HEAD（symref→裸 sha）同样被快照捕获（比较的是文件全文，不是 symref target）；
//   ④ 普通仓库快路径不受影响；
//   ⑤ bt_get_revision_headers 空 oid 数组必失败——RevisionsDataSource 全命中缓存页守卫
//      （不再对空数组发起调用）所依赖的 native 契约；
//   ⑥ ReferenceStorage 快照在 New/WithHead 两个构造路径上的传递。
using System;
using System.IO;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using Xunit;

namespace ForkPlus.Tests
{
	public class WorktreeRefreshShortCircuitTests
	{
		// CreateWithWorktree：parent（main/feature/one/feature/two）+ feature/one 检出为
		// root/parent-worktrees/wt-one。返回 (parentRoot, wtPath)。
		private static (string parent, string wt) MakeWorktreeRepo()
		{
			string parent = TestRepoFactory.CreateWithWorktree();
			string root = Path.GetDirectoryName(parent);
			return (parent, Path.Combine(root, "parent-worktrees", "wt-one"));
		}

		private static GitCommandResult<RepositoryReferences> Refresh(GitModule module, CommitGraphCache cache, RepositoryReferences old = null)
		{
			return new RefreshRepositoryReferencesGitCommand().Execute(
				module, new GitConfig(new GitConfig.Section[0]), old,
				new SubdomainsToReload(SubDomain.DefaultRefresh), cache);
		}

		private static void AssertActiveBranch(ReferenceStorage storage, string expectedRef)
		{
			Assert.True(storage.ActiveBranchIndex.HasValue, "ActiveBranchIndex 应指向分支，实际为 null（detached）");
			Assert.Equal(expectedRef, storage.Refs[storage.ActiveBranchIndex.Value]);
		}

		[Fact]
		public void Worktree_UnchangedHead_SecondRefreshReusesSameReferences()
		{
			(string parent, string wt) = MakeWorktreeRepo();
			try
			{
				var module = new GitModule(wt, Path.Combine(parent, ".git"), Path.Combine(parent, ".git", "worktrees", "wt-one"), null);
				using var cache = new CommitGraphCache(module);
				GitCommandResult<RepositoryReferences> first = Refresh(module, cache);
				Assert.True(first.Succeeded, first.Error?.FriendlyDescription ?? "first refresh failed");
				AssertActiveBranch(first.Result.ReferenceStorage, "refs/heads/feature/one");
				Assert.Equal("ref: refs/heads/feature/one", first.Result.ReferenceStorage.HeadSnapshot);

				// 核心回归断言：HEAD 未变 → 复用同一实例。修复前（worktree 禁用快路径）
				// 这里必为不同实例 → 下游全量重建风暴。
				GitCommandResult<RepositoryReferences> second = Refresh(module, cache, first.Result);
				Assert.True(second.Succeeded);
				Assert.Same(first.Result.ReferenceStorage, second.Result.ReferenceStorage);
			}
			finally
			{
				TestRepoFactory.Cleanup(Path.GetDirectoryName(parent));
			}
		}

		[Fact]
		public void Worktree_BranchSwitch_RebuildsWithNewActiveBranchThenShortCircuits()
		{
			(string parent, string wt) = MakeWorktreeRepo();
			try
			{
				var module = new GitModule(wt, Path.Combine(parent, ".git"), Path.Combine(parent, ".git", "worktrees", "wt-one"), null);
				using var cache = new CommitGraphCache(module);
				GitCommandResult<RepositoryReferences> first = Refresh(module, cache);
				Assert.True(first.Succeeded);

				// worktree 内切换分支：HEAD 文件变了（common refs 的 hash 不变——bt hash 只含
				// common refs + 主 worktree HEAD，这正是历史 bug 的成因），必须重建。
				TestRepoFactory.GitOutput(wt, "checkout -q feature/two");
				GitCommandResult<RepositoryReferences> second = Refresh(module, cache, first.Result);
				Assert.True(second.Succeeded);
				Assert.NotSame(first.Result.ReferenceStorage, second.Result.ReferenceStorage);
				AssertActiveBranch(second.Result.ReferenceStorage, "refs/heads/feature/two");
				Assert.Equal("ref: refs/heads/feature/two", second.Result.ReferenceStorage.HeadSnapshot);

				// 重建后快路径重新挂上：再刷一次 → 同实例。
				GitCommandResult<RepositoryReferences> third = Refresh(module, cache, second.Result);
				Assert.True(third.Succeeded);
				Assert.Same(second.Result.ReferenceStorage, third.Result.ReferenceStorage);
			}
			finally
			{
				TestRepoFactory.Cleanup(Path.GetDirectoryName(parent));
			}
		}

		[Fact]
		public void Worktree_DetachedHeadSwitch_IsDetectedBySnapshot()
		{
			(string parent, string wt) = MakeWorktreeRepo();
			try
			{
				var module = new GitModule(wt, Path.Combine(parent, ".git"), Path.Combine(parent, ".git", "worktrees", "wt-one"), null);
				using var cache = new CommitGraphCache(module);
				GitCommandResult<RepositoryReferences> first = Refresh(module, cache);
				Assert.True(first.Succeeded);

				// symref → 裸 sha：快照比较的是 HEAD 文件全文，detached 切换必须被发现
				//（若只比较 symref target，detached 会被误判"未变"而复用旧引用）。
				TestRepoFactory.GitOutput(wt, "checkout -q --detach");
				GitCommandResult<RepositoryReferences> second = Refresh(module, cache, first.Result);
				Assert.True(second.Succeeded);
				Assert.NotSame(first.Result.ReferenceStorage, second.Result.ReferenceStorage);
				Assert.Null(second.Result.ReferenceStorage.ActiveBranchIndex);
				string snapshot = second.Result.ReferenceStorage.HeadSnapshot;
				Assert.NotNull(snapshot);
				Assert.Equal(40, snapshot.Length);

				// detached 状态下 HEAD 未变 → 同样走快路径。
				GitCommandResult<RepositoryReferences> third = Refresh(module, cache, second.Result);
				Assert.True(third.Succeeded);
				Assert.Same(second.Result.ReferenceStorage, third.Result.ReferenceStorage);
			}
			finally
			{
				TestRepoFactory.Cleanup(Path.GetDirectoryName(parent));
			}
		}

		[Fact]
		public void Repository_Unchanged_SecondRefreshStillReuses()
		{
			// 普通仓库（主 worktree）：本次修复动了快路径条件，守卫原路径不回归。
			string parent = TestRepoFactory.CreateWithWorktree();
			try
			{
				var module = new GitModule(parent, Path.Combine(parent, ".git"), null, null);
				using var cache = new CommitGraphCache(module);
				GitCommandResult<RepositoryReferences> first = Refresh(module, cache);
				Assert.True(first.Succeeded, first.Error?.FriendlyDescription ?? "first refresh failed");
				AssertActiveBranch(first.Result.ReferenceStorage, "refs/heads/main");
				// 非 worktree：hash 已覆盖 HEAD，无需快照（null）。
				Assert.Null(first.Result.ReferenceStorage.HeadSnapshot);

				GitCommandResult<RepositoryReferences> second = Refresh(module, cache, first.Result);
				Assert.True(second.Succeeded);
				Assert.Same(first.Result.ReferenceStorage, second.Result.ReferenceStorage);
			}
			finally
			{
				TestRepoFactory.Cleanup(Path.GetDirectoryName(parent));
			}
		}

		[Fact]
		public void GetRevisionHeaders_EmptyShaArray_Fails()
		{
			// RevisionsDataSource 全命中缓存页守卫的依据：bt_get_revision_headers 对空 oid
			// 数组返回 "invalid input"（2026-10 用户日志 3 分钟 291 次该错误即此契约被踩）。
			// 若未来 native 层改为接受空数组并返回空结果，该守卫可放宽——此断言即契约哨兵。
			string parent = TestRepoFactory.CreateWithWorktree();
			try
			{
				var module = new GitModule(parent, Path.Combine(parent, ".git"), null, null);
				GitCommandResult<RevisionHeader[]> result = new GetRevisionHeadersGitCommand().Execute(module, new Sha[0]);
				Assert.False(result.Succeeded, "空 oid 数组应失败（bt 'invalid input' 契约），若已放宽请同步审视 RevisionsDataSource 守卫");
			}
			finally
			{
				TestRepoFactory.Cleanup(Path.GetDirectoryName(parent));
			}
		}
	}

	// ReferenceStorage.HeadSnapshot 传递（纯逻辑，不依赖仓库）。
	public class ReferenceStorageHeadSnapshotTests
	{
		private static ReferenceStorage MakeStorage(string headSnapshot)
		{
			return ReferenceStorage.New(
				new[] { "refs/heads/main", "HEAD" },
				new[] { new Sha(0x11111111u, 2u, 3u, 4u, 5u), new Sha(0x11111111u, 2u, 3u, 4u, 5u) },
				0uL,
				new[] { DateTime.UnixEpoch, DateTime.UnixEpoch },
				new[] { "HEAD" },
				new[] { "refs/heads/main" },
				new ReferenceStorage.UpstreamTrackingReference[0],
				0,
				headSnapshot);
		}

		[Fact]
		public void New_HeadSnapshot_Preserved()
		{
			ReferenceStorage storage = MakeStorage("ref: refs/heads/main");
			Assert.Equal("ref: refs/heads/main", storage.HeadSnapshot);
			// 默认参数路径（既有 6 个调用点兼容性）：不传 → null。
			Assert.Null(MakeStorage(null).HeadSnapshot);
		}

		[Fact]
		public void WithHead_HeadSnapshot_Preserved()
		{
			// WithHead 只替换 HEAD 条目的 sha，仓库 HEAD 文件本身没变——快照必须跟着
			// 原实例走，否则下一次刷新会误判"HEAD 变了"白重建一次。
			ReferenceStorage storage = MakeStorage("ref: refs/heads/main");
			ReferenceStorage withHead = storage.WithHead(new Sha(0x22222222u, 2u, 3u, 4u, 5u));
			Assert.Equal("ref: refs/heads/main", withHead.HeadSnapshot);
		}
	}
}
