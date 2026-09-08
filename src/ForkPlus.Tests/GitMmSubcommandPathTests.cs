using System;
using System.IO;
using ForkPlus.Git.Interaction;
using ForkPlus.Settings;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// git mm 子命令可见性回归测试（2026-09-07，用户报告：Linux 命令行 `git mm sync` 可用，
	/// GUI 弹 "git: 'mm' is not a git command"，见 "初始化 git mm 仓库" 对话框）。
	///
	/// 根因三层：
	/// ① App.GitMmPath 硬编码查找 git-mm.exe——Unix 上可执行文件无扩展名，永远找不到；
	/// ② git 查找 mm 这类自定义子命令沿"自身 exec-path + 进程 PATH"——GUI 的自带 git 实例
	///    （gitInstance/2.50.1）exec-path 与命令行的系统 git 不同（企业 git-mm 常装系统 git 的
	///    git-core 目录），桌面启动的 GUI 进程 PATH 又可能缺 ~/.local/bin 等用户 bin——两处差异
	///    都会让"命令行可用、GUI 不可用"（沙盒已实证复现）；
	/// ③ ②修复后用户实测仍复现——git-mm 装在 nvm（~/.nvm/versions/node/*/bin）、.bashrc 里
	///    export 进 PATH 的 /opt 目录、Homebrew shellenv 目录：GUI 进程不执行 shell 初始化
	///    文件，exec-path 与用户 bin 探测全部落空（见第 4 节用例）。
	///
	/// 修复：跨平台可执行名 + 系统位置探测（各 git 的 exec-path / 用户 bin / 用户 shell 环境）+
	/// GitRequest psi 路径注入 PrependGitMmDirectoryToPath（Bt 路径同一 helper）。防线：命名断言 +
	/// 注入幂等性 + 端到端（git-mm 装在 PATH 外目录，`git mm version` 仍成功——修复前此用例红：
	/// git 子进程 PATH 不含该目录，报 not a git command）+ shell 探测解析/进程两级用例。
	///
	/// 端到端经 GitMmInstancePath（用户设置，解析链最高优先级）指定 PATH 外的 fake git-mm，
	/// 不污染系统目录——exec-path/用户 bin/shell 探测（GitMmPathFromSystemLocations）是进程级缓存
	/// 且有全局副作用（会让 missing 警告测试失效），无法在共享测试进程里安全验证。
	/// 同属 "HeadlessAvalonia" 集合：与 E2e17WorkflowTests（missing 警告依赖 GitMmPath==null）
	/// 串行，避免全局 ForkPlusSettings 竞态。
	/// </summary>
	[Collection("HeadlessAvalonia")]
	public class GitMmSubcommandPathTests
	{
		// ============================ 1) 跨平台可执行名 ============================

		[Fact]
		public void GitMmExecutableName_IsPlatformCorrect()
		{
			// Unix 无扩展名——原硬编码 "git-mm.exe" 是 GUI 在 Linux 上找不到 git-mm 的根因之一
			Assert.Equal(OperatingSystem.IsWindows() ? "git-mm.exe" : "git-mm", App.GitMmExecutableName);
		}

		// ============================ 2) PATH 注入纯函数 ============================

		[Fact]
		public void PrependGitMmDirectoryToPath_InjectsWhenDirMissing()
		{
			string root = CreateTempRoot();
			try
			{
				string fakeGitMm = WriteFakeGitMmFile(root);
				string saved = ForkPlusSettings.Default.GitMmInstancePath;
				try
				{
					ForkPlusSettings.Default.GitMmInstancePath = fakeGitMm;
					string path = App.PrependGitMmDirectoryToPath("/usr/bin:/bin");
					Assert.True(path != null, "git-mm 目录不在 PATH 中时应注入");
					// 注入目录前置于首段（优先于原有段，防 PATH 里的同名旧版抢先）
					Assert.StartsWith(Path.GetDirectoryName(fakeGitMm) + Path.PathSeparator, path);
					// 原有段保留
					Assert.EndsWith("/usr/bin:/bin", path);
				}
				finally
				{
					ForkPlusSettings.Default.GitMmInstancePath = saved;
				}
			}
			finally
			{
				TryDelete(root);
			}
		}

		[Fact]
		public void PrependGitMmDirectoryToPath_IdempotentWhenDirPresent()
		{
			string root = CreateTempRoot();
			try
			{
				string fakeGitMm = WriteFakeGitMmFile(root);
				string saved = ForkPlusSettings.Default.GitMmInstancePath;
				try
				{
					ForkPlusSettings.Default.GitMmInstancePath = fakeGitMm;
					string gitMmDir = Path.GetDirectoryName(fakeGitMm);
					// 目录已在 PATH（含非首段）→ 无需注入，返回 null
					Assert.Null(App.PrependGitMmDirectoryToPath("/usr/bin" + Path.PathSeparator + gitMmDir + Path.PathSeparator + "/bin"));
					Assert.Null(App.PrependGitMmDirectoryToPath(gitMmDir));
					// 段级比较：目录名互为子串不误判为已包含
					string siblingDir = gitMmDir + "-suffix";
					string injected = App.PrependGitMmDirectoryToPath(siblingDir);
					Assert.True(injected != null, "仅子串相同（非同段）时应注入");
					Assert.StartsWith(gitMmDir + Path.PathSeparator, injected);
				}
				finally
				{
					ForkPlusSettings.Default.GitMmInstancePath = saved;
				}
			}
			finally
			{
				TryDelete(root);
			}
		}

		[Fact]
		public void PrependGitMmDirectoryToPath_EmptyPathGetsDirOnly()
		{
			string root = CreateTempRoot();
			try
			{
				string fakeGitMm = WriteFakeGitMmFile(root);
				string saved = ForkPlusSettings.Default.GitMmInstancePath;
				try
				{
					ForkPlusSettings.Default.GitMmInstancePath = fakeGitMm;
					// 空/缺失 PATH（极端桌面环境）→ 只剩注入目录本身
					Assert.Equal(Path.GetDirectoryName(fakeGitMm), App.PrependGitMmDirectoryToPath(null));
					Assert.Equal(Path.GetDirectoryName(fakeGitMm), App.PrependGitMmDirectoryToPath(""));
				}
				finally
				{
					ForkPlusSettings.Default.GitMmInstancePath = saved;
				}
			}
			finally
			{
				TryDelete(root);
			}
		}

		// ============================ 3) 端到端：git mm 经注入的 PATH 找到 git-mm ============================

		[Fact]
		public void GitRequest_Execute_GitMm_FindsExecutableOutsidePath()
		{
			// fake git-mm 是 sh 脚本，仅 Unix 可执行（与 E2e17 MakeFakeGitMm 同口径；CI 为 ubuntu）
			if (OperatingSystem.IsWindows())
			{
				return;
			}
			Assert.True(File.Exists(App.GitPath), "环境前置：App.GitPath 应存在（gitInstance）");

			string root = CreateTempRoot();
			try
			{
				// fake git-mm 装在测试进程 PATH 之外的目录——模拟用户场景
				// （系统 git exec-path / 用户 bin：命令行 git 找得到，GUI 的 git 实例 + 进程 PATH 找不到）
				string fakeGitMm = WriteFakeGitMmFile(root);
				string saved = ForkPlusSettings.Default.GitMmInstancePath;
				try
				{
					ForkPlusSettings.Default.GitMmInstancePath = fakeGitMm;
					GitRequestResult result = default(GitRequest)
						.Command(new GitCommand("mm", "version"))
						.CurrentDir(root)
						.Execute();
					// 修复前：git 子进程 PATH 不含 fake 目录 → "git: 'mm' is not a git command"
					Assert.True(result.Success, "git mm 应经注入的 PATH 找到 git-mm。ExitCode=" + result.ExitCode + " Stderr: " + result.Stderr);
					Assert.Contains("git-mm version 3.2.0", result.Stdout);
				}
				finally
				{
					ForkPlusSettings.Default.GitMmInstancePath = saved;
				}
			}
			finally
			{
				TryDelete(root);
			}
		}

		// ============================ 4) shell 环境探测（残余盲区：nvm/.bashrc 专属 PATH） ============================
		// 首轮修复（exec-path + 用户 bin）后用户实测仍复现：git-mm 装在 nvm
		//（~/.nvm/versions/node/*/bin）或 .bashrc 里 export 进 PATH 的 /opt 目录、Homebrew
		// shellenv 目录——GUI 进程不执行 shell 初始化文件，三处常规探测全部落空。
		// 修复：FindExecutableInShellEnvironment 以"用户 shell 会看到什么"的口径兜底。

		[Fact]
		public void MatchExecutablePathInShellOutput_ParsesValidLineAmongNoise()
		{
			string root = CreateTempRoot();
			try
			{
				string fakeGitMm = WriteFakeGitMmFile(root);
				// rc 杂音（motd / 别名定义 / 相对路径 / 函数名）与有效路径混合，逐行过滤后应命中有效行
				string stdout = "Welcome to bash 5.2\nalias git-mm='echo hi'\ngit-mm\n" + fakeGitMm + "\n";
				Assert.Equal(fakeGitMm, App.MatchExecutablePathInShellOutput(stdout, App.GitMmExecutableName));
			}
			finally
			{
				TryDelete(root);
			}
		}

		[Fact]
		public void MatchExecutablePathInShellOutput_RejectsNonMatchingOutput()
		{
			string root = CreateTempRoot();
			try
			{
				// 文件名不完全一致（前缀相同）→ 不采用（防杂音行误命中）
				string decoy = Path.Combine(root, "git-mm-old");
				File.WriteAllText(decoy, "");
				Assert.Null(App.MatchExecutablePathInShellOutput(decoy, App.GitMmExecutableName));
				// 不存在的路径 / 空输出 → null
				Assert.Null(App.MatchExecutablePathInShellOutput("/usr/nonexistent/git-mm", App.GitMmExecutableName));
				Assert.Null(App.MatchExecutablePathInShellOutput("", App.GitMmExecutableName));
				Assert.Null(App.MatchExecutablePathInShellOutput(null, App.GitMmExecutableName));
			}
			finally
			{
				TryDelete(root);
			}
		}

		[Fact]
		public void FindExecutableInShellEnvironment_FakeShellProbeFindsExecutable()
		{
			// fake shell 是 sh 脚本，仅 Unix 可执行（CI 为 ubuntu）
			if (OperatingSystem.IsWindows())
			{
				return;
			}
			string root = CreateTempRoot();
			try
			{
				// fake git-mm 装在"PATH 外"目录（模拟 nvm/.bashrc 专属 bin），探测只能靠 shell 输出拿到
				string fakeGitMm = WriteFakeGitMmFile(root);
				string fakeShell = Path.Combine(root, "fakeshell");
				File.WriteAllText(fakeShell, "#!/bin/sh\necho " + fakeGitMm + "\n");
				MakeExecutable(fakeShell);
				string found = App.FindExecutableInShellEnvironment(App.GitMmExecutableName, new string[1] { fakeShell });
				Assert.Equal(fakeGitMm, found);
			}
			finally
			{
				TryDelete(root);
			}
		}

		[Fact]
		public void FindExecutableInShellEnvironment_InvalidCandidatesReturnNull()
		{
			// shell 候选不存在 / 列表为 null → 安全返回 null（真实场景换下个候选，此处无候选）
			Assert.Null(App.FindExecutableInShellEnvironment(App.GitMmExecutableName, new string[1] { "/nonexistent/shell" }));
			Assert.Null(App.FindExecutableInShellEnvironment(App.GitMmExecutableName, null));
			// 候选 shell 存在但输出无匹配（echo 杂音）→ null
			Assert.Null(App.FindExecutableInShellEnvironment(App.GitMmExecutableName, new string[1] { "/bin/echo" }));
		}

		// ============================ helpers ============================

		private static string CreateTempRoot()
		{
			string root = Path.Combine(Path.GetTempPath(), "fpgitmmpath_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(root);
			return root;
		}

		/// <summary>写一个仅输出版本号的 fake git-mm（Unix 上 chmod +x——端到端用例里 git 要真正执行它；
		/// Windows 无 chmod，纯函数用例只查存在性，端到端用例已平台守卫跳过）。</summary>
		private static string WriteFakeGitMmFile(string root)
		{
			string path = Path.Combine(root, App.GitMmExecutableName);
			File.WriteAllText(path, "#!/bin/sh\necho \"git-mm version 3.2.0\"\n");
			MakeExecutable(path);
			return path;
		}

		/// <summary>Unix 上给脚本加可执行位（fake git-mm / fake shell 共用）；Windows 无操作。</summary>
		private static void MakeExecutable(string path)
		{
			if (!OperatingSystem.IsWindows())
			{
				System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("chmod", "+x " + path)).WaitForExit();
			}
		}

		private static void TryDelete(string root)
		{
			try
			{
				Directory.Delete(root, recursive: true);
			}
			catch
			{
			}
		}
	}
}
