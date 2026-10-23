// BugFix：链接 worktree 误显示 "detached HEAD"（v2.x）——纯逻辑版，无需 UI。
//
// 根因：native biturbo 的 bt_get_references 手工扫 <gitdir>/refs + packed-refs，不解析
// commondir——链接 worktree 的 gitdir（.git/worktrees/<name>）里没有 refs/（全在 common dir），
// 枚举出的 symrefs 里没有任何 refs/heads/*，ReferenceStorage.New 推导 ActiveBranchIndex=null
// → UI 显示 "detached HEAD"。
//
// 修复（两步）：
//   ① GitModule.ReferencesGitDir()：worktree 用 common dir 枚举 refs（其余仓库形态不变）；
//   ② BtReferencesExtensions.ResolveRepositoryHead()：common dir 返回的 HEAD symref 是主
//      worktree 的 HEAD，按 <worktreeGitDir>/HEAD 覆盖 target；detached / 不可读时移除 HEAD
//      条目（UI 与 git 一致地显示 detached）。普通仓库 / 子模块走原 EnsureHeadSymref 逻辑。
//
// 本文件覆盖 ② 的全部分支与 ① 的目录选择，并用 ReferenceStorage.New 验证修复后
// ActiveBranchIndex 能正确指向本 worktree 的分支（修复前恒 null）。
using System;
using System.IO;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using Xunit;

namespace ForkPlus.Tests
{
	public class BtReferencesHeadResolutionTests
	{
		private static string MakeDir()
		{
			string dir = Path.Combine(Path.GetTempPath(), "ForkPlusTests", "BtRefsHead", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(dir);
			return dir;
		}

		private static void WriteHead(string gitDir, string content)
		{
			Directory.CreateDirectory(gitDir);
			File.WriteAllText(Path.Combine(gitDir, "HEAD"), content);
		}

		private static void Cleanup(params string[] dirs)
		{
			foreach (string dir in dirs)
			{
				try
				{
					Directory.Delete(dir, recursive: true);
				}
				catch (IOException)
				{
				}
				catch (UnauthorizedAccessException)
				{
				}
			}
		}

		[Fact]
		public void ReferencesGitDir_Worktree_UsesCommonDir()
		{
			string root = MakeDir();
			try
			{
				string commonGitDir = Path.Combine(root, ".git");
				string worktreeGitDir = Path.Combine(commonGitDir, "worktrees", "wt");
				var worktree = new GitModule(Path.Combine(root, "wt"), commonGitDir, worktreeGitDir, null);
				var repository = new GitModule(root, commonGitDir, null, null);
				Assert.Equal(ModuleType.Worktree, worktree.Type);
				Assert.Equal(commonGitDir, worktree.ReferencesGitDir());
				Assert.Equal(ModuleType.Repository, repository.Type);
				Assert.Equal(commonGitDir, repository.ReferencesGitDir());
			}
			finally
			{
				Cleanup(root);
			}
		}

		[Fact]
		public void ResolveRepositoryHead_Worktree_ReplacesMainWorktreeHead()
		{
			string root = MakeDir();
			try
			{
				string commonGitDir = Path.Combine(root, ".git");
				string worktreeGitDir = Path.Combine(commonGitDir, "worktrees", "wt");
				// 主 worktree 在 feature_hebin，本 worktree 在 feature——bt 从 common dir 返回的是
				// 主 worktree 的 HEAD（错的），必须按本 worktree 的 HEAD 覆盖。
				WriteHead(commonGitDir, "ref: refs/heads/feature_hebin\n");
				WriteHead(worktreeGitDir, "ref: refs/heads/feature\n");
				var module = new GitModule(Path.Combine(root, "wt"), commonGitDir, worktreeGitDir, null);
				string[] symrefs = { "HEAD", "refs/remotes/origin/HEAD" };
				string[] targets = { "refs/heads/feature_hebin", "refs/heads/master" };
				(string[] fixedSymrefs, string[] fixedTargets) = BtReferencesExtensions.ResolveRepositoryHead(module, symrefs, targets);
				Assert.Equal(2, fixedSymrefs.Length);
				Assert.Equal("HEAD", fixedSymrefs[0]);
				Assert.Equal("refs/heads/feature", fixedTargets[0]);
				Assert.Equal("refs/remotes/origin/HEAD", fixedSymrefs[1]);
				Assert.Equal("refs/heads/master", fixedTargets[1]);
			}
			finally
			{
				Cleanup(root);
			}
		}

		[Fact]
		public void ResolveRepositoryHead_WorktreeDetached_DropsHeadEntry()
		{
			string root = MakeDir();
			try
			{
				string commonGitDir = Path.Combine(root, ".git");
				string worktreeGitDir = Path.Combine(commonGitDir, "worktrees", "wt");
				WriteHead(commonGitDir, "ref: refs/heads/feature_hebin\n");
				// 本 worktree 处于 detached HEAD（HEAD 是裸 sha）：必须移除 bt 返回的主 worktree
				// HEAD 条目，让 UI 显示 detached 而不是主 worktree 的分支名。
				WriteHead(worktreeGitDir, "0123456789012345678901234567890123456789\n");
				var module = new GitModule(Path.Combine(root, "wt"), commonGitDir, worktreeGitDir, null);
				string[] symrefs = { "HEAD", "refs/remotes/origin/HEAD" };
				string[] targets = { "refs/heads/feature_hebin", "refs/heads/master" };
				(string[] fixedSymrefs, string[] fixedTargets) = BtReferencesExtensions.ResolveRepositoryHead(module, symrefs, targets);
				Assert.Single(fixedSymrefs);
				Assert.Equal("refs/remotes/origin/HEAD", fixedSymrefs[0]);
				Assert.Equal("refs/heads/master", fixedTargets[0]);
			}
			finally
			{
				Cleanup(root);
			}
		}

		[Fact]
		public void ResolveRepositoryHead_WorktreeMissingHead_AppendsFromWorktreeHead()
		{
			string root = MakeDir();
			try
			{
				string commonGitDir = Path.Combine(root, ".git");
				string worktreeGitDir = Path.Combine(commonGitDir, "worktrees", "wt");
				WriteHead(worktreeGitDir, "ref: refs/heads/feature\n");
				var module = new GitModule(Path.Combine(root, "wt"), commonGitDir, worktreeGitDir, null);
				// Linux 上 bt_get_references 不返回 HEAD symref：按本 worktree 的 HEAD 补齐。
				string[] symrefs = { "refs/remotes/origin/HEAD" };
				string[] targets = { "refs/heads/master" };
				(string[] fixedSymrefs, string[] fixedTargets) = BtReferencesExtensions.ResolveRepositoryHead(module, symrefs, targets);
				Assert.Equal(2, fixedSymrefs.Length);
				Assert.Equal("refs/remotes/origin/HEAD", fixedSymrefs[0]);
				Assert.Equal("refs/heads/master", fixedTargets[0]);
				Assert.Equal("HEAD", fixedSymrefs[1]);
				Assert.Equal("refs/heads/feature", fixedTargets[1]);
			}
			finally
			{
				Cleanup(root);
			}
		}

		[Fact]
		public void ResolveRepositoryHead_Repository_KeepsBtHeadAndAppendsWhenMissing()
		{
			string root = MakeDir();
			try
			{
				string commonGitDir = Path.Combine(root, ".git");
				WriteHead(commonGitDir, "ref: refs/heads/master\n");
				var module = new GitModule(root, commonGitDir, null, null);
				// bt 已返回 HEAD（Windows）：原样保留。
				string[] symrefs = { "HEAD" };
				string[] targets = { "refs/heads/master" };
				(string[] keptSymrefs, string[] keptTargets) = BtReferencesExtensions.ResolveRepositoryHead(module, symrefs, targets);
				Assert.Same(symrefs, keptSymrefs);
				Assert.Same(targets, keptTargets);
				// bt 未返回 HEAD（Linux）：按 .git/HEAD 补齐。
				(string[] appendedSymrefs, string[] appendedTargets) = BtReferencesExtensions.ResolveRepositoryHead(module, new string[0], new string[0]);
				Assert.Single(appendedSymrefs);
				Assert.Equal("HEAD", appendedSymrefs[0]);
				Assert.Equal("refs/heads/master", appendedTargets[0]);
			}
			finally
			{
				Cleanup(root);
			}
		}

		[Fact]
		public void ResolveRepositoryHead_Worktree_ActiveBranchIndexPointsToWorktreeBranch()
		{
			string root = MakeDir();
			try
			{
				string commonGitDir = Path.Combine(root, ".git");
				string worktreeGitDir = Path.Combine(commonGitDir, "worktrees", "wt");
				WriteHead(commonGitDir, "ref: refs/heads/feature_hebin\n");
				WriteHead(worktreeGitDir, "ref: refs/heads/feature\n");
				var module = new GitModule(Path.Combine(root, "wt"), commonGitDir, worktreeGitDir, null);
				// 模拟修复后的完整链路：common dir 的 refs + 主 worktree 的 HEAD symref
				// → ResolveRepositoryHead 修正 → ReferenceStorage.New 推导 ActiveBranchIndex。
				string[] refs = { "refs/heads/feature", "refs/heads/feature_hebin" };
				string[] symrefs = { "HEAD" };
				string[] targets = { "refs/heads/feature_hebin" };
				(string[] fixedSymrefs, string[] fixedTargets) = BtReferencesExtensions.ResolveRepositoryHead(module, symrefs, targets);
				ReferenceStorage storage = ReferenceStorage.New(
					refs,
					new[] { new Sha(0x11111111u, 2u, 3u, 4u, 5u), new Sha(0x22222222u, 2u, 3u, 4u, 5u) },
					0uL,
					new[] { DateTime.UnixEpoch, DateTime.UnixEpoch },
					fixedSymrefs,
					fixedTargets,
					new ReferenceStorage.UpstreamTrackingReference[0],
					0);
				// 修复前：worktree gitdir 枚举不到 refs/heads/*，ActiveBranchIndex 恒 null → "detached HEAD"。
				Assert.True(storage.ActiveBranchIndex.HasValue, "ActiveBranchIndex 应指向本 worktree 的分支，而不是 null（detached）");
				Assert.Equal(0, storage.ActiveBranchIndex.Value);
				Assert.Equal("refs/heads/feature", storage.Refs[storage.ActiveBranchIndex.Value]);
			}
			finally
			{
				Cleanup(root);
			}
		}
	}
}
