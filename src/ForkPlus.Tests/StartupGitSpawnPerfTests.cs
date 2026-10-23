using System;
using System.IO;
using ForkPlus.Git;
using ForkPlus.Git.Interaction;
using Xunit;

namespace ForkPlus.Tests
{
	// 性能回归（2026-10，"打开仓库首屏轨道图慢"）：
	// 旧版空仓库检测在首屏关键路径 spawn git rev-parse --verify HEAD（RefreshRepositoryReferences
	// 与 GetRepositoryState 各一次）。在进程创建被终端安全软件拦截的机器上实测一次 ~2.4s，
	// 3.3s 的首屏数据管线里它占 2.4s——这是"原版 Fork 快、ForkPlus 慢"的主因（原版关键路径
	// 全程走 bt 原生调用，零 git spawn）。修复后两处均改为零子进程检测，本测试守卫其判定语义：
	// GitModule.IsHeadUnborn 必须与旧版 rev-parse --verify HEAD 语义等价
	//（unborn HEAD ⇔ rev-parse 失败 ⇔ 空仓库），且 worktree/commondir 布局判定正确。
	// 全部用手工构造的 .git 文件布局，不 spawn git（本机 spawn 一次 ~1-2s，测试跑不起）。

	public class GitModuleHeadUnbornTests
	{
		private static string NewTempDir()
		{
			string root = Path.Combine(Path.GetTempPath(), "fpunborn_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(root);
			return root;
		}

		private static GitModule CreateModule(string headContent, Action<string> writeRefs = null)
		{
			string root = NewTempDir();
			string gitDir = Path.Combine(root, ".git");
			Directory.CreateDirectory(gitDir);
			if (headContent != null)
			{
				File.WriteAllText(Path.Combine(gitDir, "HEAD"), headContent);
			}
			writeRefs?.Invoke(gitDir);
			return GitModule.Create(root, gitDir, null, null);
		}

		[Fact]
		public void HeadUnborn_GitInitState_IsUnborn()
		{
			// git init 完毕：HEAD = ref: refs/heads/main，分支不存在（loose + packed-refs 均无）
			GitModule module = CreateModule("ref: refs/heads/main\n");
			Assert.True(module.IsHeadUnborn());
		}

		[Fact]
		public void HeadUnborn_LooseRefExists_NotUnborn()
		{
			GitModule module = CreateModule("ref: refs/heads/develop\n", delegate (string gitDir)
			{
				string looseRef = Path.Combine(gitDir, "refs", "heads", "develop");
				Directory.CreateDirectory(Path.GetDirectoryName(looseRef));
				File.WriteAllText(looseRef, new string('0', 40) + "\n");
			});
			Assert.False(module.IsHeadUnborn());
		}

		[Fact]
		public void HeadUnborn_NestedLooseRefExists_NotUnborn()
		{
			// 分支名带斜杠：refs/heads/feature/one → .git/refs/heads/feature/one
			GitModule module = CreateModule("ref: refs/heads/feature/one\n", delegate (string gitDir)
			{
				string looseRef = Path.Combine(gitDir, "refs", "heads", "feature", "one");
				Directory.CreateDirectory(Path.GetDirectoryName(looseRef));
				File.WriteAllText(looseRef, new string('1', 40) + "\n");
			});
			Assert.False(module.IsHeadUnborn());
		}

		[Fact]
		public void HeadUnborn_PackedRefExists_NotUnborn()
		{
			GitModule module = CreateModule("ref: refs/heads/develop\n", delegate (string gitDir)
			{
				File.WriteAllText(Path.Combine(gitDir, "packed-refs"),
					"# pack-refs with: peeled fully-peeled sorted \n" +
					new string('2', 40) + " refs/heads/develop\n" +
					new string('3', 40) + " refs/heads/master\n" +
					new string('4', 40) + " refs/tags/v1.0\n" +
					"^" + new string('5', 40) + "\n");
			});
			Assert.False(module.IsHeadUnborn());
		}

		[Fact]
		public void HeadUnborn_PackedRefPrefixTrap_StillUnborn()
		{
			// 前缀陷阱守卫（曾写错为 EndsWith(target)）：packed-refs 里只有
			// refs/heads/feature/master，HEAD 目标 refs/heads/master 不存在——
			// 不得因后缀相同误判为存在。
			GitModule module = CreateModule("ref: refs/heads/master\n", delegate (string gitDir)
			{
				File.WriteAllText(Path.Combine(gitDir, "packed-refs"),
					new string('6', 40) + " refs/heads/feature/master\n");
			});
			Assert.True(module.IsHeadUnborn());
		}

		[Fact]
		public void HeadUnborn_DetachedHead_NotUnborn()
		{
			// detached HEAD（40-hex）：git 不会写出指向无效对象的 detached HEAD，保守走原流程
			GitModule module = CreateModule(new string('a', 40) + "\n");
			Assert.False(module.IsHeadUnborn());
		}

		[Fact]
		public void HeadUnborn_MissingHeadFile_NotUnborn()
		{
			// HEAD 读取失败：保守视为非空（与旧版 catch 行为一致，让原流程继续走）
			GitModule module = CreateModule(null);
			Assert.False(module.IsHeadUnborn());
		}

		[Fact]
		public void HeadUnborn_WorktreeRefsInCommonDir_NotUnborn()
		{
			// worktree 场景：HEAD 在 worktreeGitDir，refs/heads 在 common dir——
			// 分支只在 common dir 的 packed-refs 里，必须命中（否则 worktree 误判 unborn
			// → 误走空仓库快速路径，分支列表全空）。
			string root = NewTempDir();
			string commonDir = Path.Combine(root, "common");
			string worktreeGitDir = Path.Combine(root, "wt");
			Directory.CreateDirectory(commonDir);
			Directory.CreateDirectory(worktreeGitDir);
			File.WriteAllText(Path.Combine(worktreeGitDir, "HEAD"), "ref: refs/heads/develop\n");
			File.WriteAllText(Path.Combine(commonDir, "packed-refs"),
				new string('7', 40) + " refs/heads/develop\n");
			GitModule module = GitModule.Create(root, commonDir, worktreeGitDir, null);
			Assert.False(module.IsHeadUnborn());
		}

		[Fact]
		public void HeadUnborn_WorktreeUnbornInWorktree_IsUnborn()
		{
			// 反向：worktree 的 HEAD unborn（切到不存在的分支），common dir 有别的分支——
			// 判定只看 HEAD 目标分支，必须判 unborn。
			string root = NewTempDir();
			string commonDir = Path.Combine(root, "common");
			string worktreeGitDir = Path.Combine(root, "wt");
			Directory.CreateDirectory(commonDir);
			Directory.CreateDirectory(worktreeGitDir);
			File.WriteAllText(Path.Combine(worktreeGitDir, "HEAD"), "ref: refs/heads/orphan-branch\n");
			File.WriteAllText(Path.Combine(commonDir, "packed-refs"),
				new string('8', 40) + " refs/heads/main\n");
			GitModule module = GitModule.Create(root, commonDir, worktreeGitDir, null);
			Assert.True(module.IsHeadUnborn());
		}
	}

	// shim 解析守卫（同轮性能修复）：GitRequest spawn 前把 <root>\bin\git.exe /
	// <root>\cmd\git.exe 薄壳解析为 <root>\mingw64\bin\git.exe 真实 exe（每次 git 调用
	// 省一次进程创建，实测 shim ≈ 2.1s vs 直连 ≈ 1.2s）。不得改写 git-mm.exe、
	// 非 bin/cmd 布局、无 mingw64 姊妹文件的路径。

	public class GitExecutableResolverTests
	{
		private static string CreateShimPath(string shimDirectoryName)
		{
			string root = Path.Combine(Path.GetTempPath(), "fpgitres_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			string shimDir = Path.Combine(root, shimDirectoryName);
			Directory.CreateDirectory(shimDir);
			return Path.Combine(shimDir, "git.exe");
		}

		[Fact]
		public void GitExecutableResolver_ResolvesBinAndCmdShims()
		{
			// Windows Git 发行版布局（bin/cmd 薄壳 → mingw64\bin\git.exe）只在 Windows 上解析；
			// 非 Windows 平台 ResolveCore 的 OS 守卫应原样返回（即使文件名 git.exe、父目录
			// bin/cmd、mingw64 姊妹文件齐备也不改写）——CI 的 core 分片在 ubuntu 上跑，
			// 不能假设在 Windows。
			foreach (string shimDirectoryName in new[] { "bin", "cmd" })
			{
				string shim = CreateShimPath(shimDirectoryName);
				string root = Path.GetDirectoryName(Path.GetDirectoryName(shim));
				string realGitDir = Path.Combine(root, "mingw64", "bin");
				Directory.CreateDirectory(realGitDir);
				string realGit = Path.Combine(realGitDir, "git.exe");
				File.WriteAllText(realGit, "real");
				if (!OperatingSystem.IsWindows())
				{
					Assert.Equal(shim, GitExecutableResolver.Resolve(shim));
					continue;
				}
				Assert.Equal(realGit, GitExecutableResolver.Resolve(shim));
			}
		}

		[Fact]
		public void GitExecutableResolver_LeavesNonShimPathsAlone()
		{
			// 无 mingw64 姊妹：原样返回
			string orphan = CreateShimPath("bin");
			Assert.Equal(orphan, GitExecutableResolver.Resolve(orphan));

			// git-mm.exe（git mm 集成的自定义可执行文件）：原样返回
			string mm = Path.Combine(Path.GetDirectoryName(orphan), "git-mm.exe");
			File.WriteAllText(mm, "mm");
			Assert.Equal(mm, GitExecutableResolver.Resolve(mm));

			// mingw64\bin\git.exe（已是真实 exe）：原样返回
			string root2 = Path.Combine(Path.GetTempPath(), "fpgitres_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			string real = Path.Combine(root2, "mingw64", "bin", "git.exe");
			Directory.CreateDirectory(Path.GetDirectoryName(real));
			File.WriteAllText(real, "real");
			Assert.Equal(real, GitExecutableResolver.Resolve(real));

			// 空路径：原样返回
			Assert.Null(GitExecutableResolver.Resolve(null));
			Assert.Equal("", GitExecutableResolver.Resolve(""));
		}
	}
}
