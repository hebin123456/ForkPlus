using System;
using System.IO;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Shell.Interaction;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// git-ai 代理模式修复回归测试（2026-09-07，用户截图：GUI 执行 git-ai stats 'fe8e3ea..HEAD' 报
	/// git: 'stats' is not a git command）。
	///
	/// 根因：git-ai 二进制按 argv[0] 的文件名分发——文件名为 git-ai（Windows 为 git-ai.exe）才走
	/// 原生命令分支（stats/diff/checkpoint/blame），其他任何文件名（手动下载的 git-ai-linux-x64、
	/// 带版本号的副本、改名安装）一律进入 git 透明代理模式，把参数原样转发给真 git——
	/// git-ai stats 于是变成 git stats，报 not a git command（沙盒以 git-ai 1.7.2 实证复现）。
	///
	/// 修复三防线：
	/// ① App.EnsureGitAiExecutionPath staging 符号链接——解析结果文件名非标准时在 staging 目录
	///    建标准名链接，argv[0] 文件名 = git-ai → 原生命令分支（核心根治）；
	/// ② GitAiVersionChecker 代理版本输出识别——代理模式 --version 输出 "git version x.y.z"，
	///    不识别会被误判为"版本正常"（>= 1.0.0），掩盖配置问题；
	/// ③ GetGitAiStatsGitCommand 代理转发症状翻译——staging 降级（如 Windows 无符号链接权限）
	///    时把 git 的原始报错翻译成可定位的提示，而不是一脸懵的 not a git command。
	///
	/// 覆盖：命名断言 + staging 行为（直通/建链/幂等/重定向/空目标）+ 代理输出识别（纯函数 + fake 二进制）+
	/// 端到端 argv[0] 分发（同一 fake 二进制：非标准名直跑进代理分支、经 staging 链接跑进原生活分支）。
	/// fake 二进制是 sh 脚本，执行类用例仅 Unix（CI 为 ubuntu，与 GitMmSubcommandPathTests 同口径）；
	/// 纯函数与 staging 链接用例跨平台（staging 依赖 File.CreateSymbolicLink，Windows 需开发者模式，
	/// 链接类用例带 Unix 守卫——EnsureGitAiExecutionPath 在建链失败时降级返回原路径，无法在 Windows
	/// CI 上断言链接必然存在）。
	/// </summary>
	public class GitAiSubcommandPathTests
	{
		// ============================ 1) 跨平台可执行名 ============================

		[Fact]
		public void GitAiExecutableName_IsPlatformCorrect()
		{
			// Unix 无扩展名——与 git-mm 同口径（App.GitAiExecutableName 供 staging 链接命名与解析复用）
			Assert.Equal(OperatingSystem.IsWindows() ? "git-ai.exe" : "git-ai", App.GitAiExecutableName);
		}

		// ============================ 2) staging 符号链接（核心修复） ============================

		[Fact]
		public void EnsureGitAiExecutionPath_StandardName_ReturnsOriginalWithoutStaging()
		{
			string root = CreateTempRoot();
			try
			{
				// 标准名可执行文件（平台正确命名）→ 原样返回，不建 staging 目录（零开销直通）
				string standard = Path.Combine(root, App.GitAiExecutableName);
				File.WriteAllText(standard, "fake git-ai binary");
				string stagingDir = Path.Combine(root, "staging");
				Assert.Equal(standard, App.EnsureGitAiExecutionPath(standard, stagingDir));
				Assert.False(Directory.Exists(stagingDir), "标准名无需 staging，不应创建 staging 目录");
			}
			finally
			{
				TryDelete(root);
			}
		}

		[Fact]
		public void EnsureGitAiExecutionPath_NullOrMissing_ReturnsAsIs()
		{
			string root = CreateTempRoot();
			try
			{
				string stagingDir = Path.Combine(root, "staging");
				// null/空/不存在的路径 → 原样返回（未安装场景，调用方自行降级）
				Assert.Null(App.EnsureGitAiExecutionPath(null, stagingDir));
				Assert.Equal("", App.EnsureGitAiExecutionPath("", stagingDir));
				string missing = Path.Combine(root, "git-ai-linux-x64");
				Assert.Equal(missing, App.EnsureGitAiExecutionPath(missing, stagingDir));
				Assert.False(Directory.Exists(stagingDir), "无可执行文件时不应创建 staging 目录");
			}
			finally
			{
				TryDelete(root);
			}
		}

		[Fact]
		public void EnsureGitAiExecutionPath_NonStandardName_StagesStandardNamedLink()
		{
			// File.CreateSymbolicLink 在 Windows 需开发者模式/管理员权限，CI 为 ubuntu——Unix 守卫
			if (OperatingSystem.IsWindows())
			{
				return;
			}
			string root = CreateTempRoot();
			try
			{
				// 非标准名（用户手动下载的 git-ai-linux-x64 典型场景）→ staging 目录建标准名链接，
				// 返回链接路径（argv[0] 文件名 = git-ai → 原生命令分支）
				string target = WriteFakeFile(root, "git-ai-linux-x64", "fake git-ai binary");
				string stagingDir = Path.Combine(root, "staging");
				string staged = App.EnsureGitAiExecutionPath(target, stagingDir);
				Assert.Equal(Path.Combine(stagingDir, App.GitAiExecutableName), staged);
				Assert.True(File.Exists(staged), "staging 链接应存在且有效");
				// 链接指向原始目标（LinkTarget 非空即符号链接，值即指向路径）
				Assert.Equal(target, new FileInfo(staged).LinkTarget);
			}
			finally
			{
				TryDelete(root);
			}
		}

		[Fact]
		public void EnsureGitAiExecutionPath_Idempotent_SameTargetReusesLinkWithoutRebuild()
		{
			if (OperatingSystem.IsWindows())
			{
				return;
			}
			string root = CreateTempRoot();
			try
			{
				string target = WriteFakeFile(root, "git-ai-linux-x64", "fake git-ai binary");
				string stagingDir = Path.Combine(root, "staging");
				string linkPath = Path.Combine(stagingDir, App.GitAiExecutableName);
				App.EnsureGitAiExecutionPath(target, stagingDir);
				// 用普通 marker 文件占住链接路径：幂等分支（目标未变且链接有效）只查 File.Exists
				// 不重建——marker 保留即证明零 IO 复用；重建分支会删掉 marker 换成真符号链接
				// （ReparsePoint 属性可区分两者）
				File.Delete(linkPath);
				File.WriteAllText(linkPath, "marker");
				string stagedAgain = App.EnsureGitAiExecutionPath(target, stagingDir);
				Assert.Equal(linkPath, stagedAgain);
				Assert.False(File.GetAttributes(linkPath).HasFlag(FileAttributes.ReparsePoint),
					"目标未变时应零 IO 复用（marker 未被替换为符号链接）");
			}
			finally
			{
				TryDelete(root);
			}
		}

		[Fact]
		public void EnsureGitAiExecutionPath_TargetChange_RetargetsLink()
		{
			if (OperatingSystem.IsWindows())
			{
				return;
			}
			string root = CreateTempRoot();
			try
			{
				// 用户在偏好设置换了自定义路径（另一个非标准名二进制）→ 链接重定向到新目标
				string targetA = WriteFakeFile(root, "git-ai-linux-x64", "fake git-ai binary A");
				string targetB = WriteFakeFile(root, "git-ai-v1.7.2", "fake git-ai binary B");
				string stagingDir = Path.Combine(root, "staging");
				string linkPath = Path.Combine(stagingDir, App.GitAiExecutableName);
				Assert.Equal(linkPath, App.EnsureGitAiExecutionPath(targetA, stagingDir));
				Assert.Equal(linkPath, App.EnsureGitAiExecutionPath(targetB, stagingDir));
				Assert.Equal(targetB, new FileInfo(linkPath).LinkTarget);
			}
			finally
			{
				TryDelete(root);
			}
		}

		[Fact]
		public void EnsureGitAiExecutionPath_StaleLink_GetsRebuilt()
		{
			if (OperatingSystem.IsWindows())
			{
				return;
			}
			string root = CreateTempRoot();
			try
			{
				// 链接被外部删除（用户清缓存/杀毒软件）→ File.Exists 落空自动重建（幂等缓存
				// 条件含链接有效性，悬空/缺失都会触发重建）
				string target = WriteFakeFile(root, "git-ai-linux-x64", "fake git-ai binary");
				string stagingDir = Path.Combine(root, "staging");
				string linkPath = Path.Combine(stagingDir, App.GitAiExecutableName);
				App.EnsureGitAiExecutionPath(target, stagingDir);
				File.Delete(linkPath);
				Assert.Equal(linkPath, App.EnsureGitAiExecutionPath(target, stagingDir));
				Assert.True(File.Exists(linkPath), "链接被删后应自动重建");
				Assert.Equal(target, new FileInfo(linkPath).LinkTarget);
			}
			finally
			{
				TryDelete(root);
			}
		}

		// ============================ 3) 版本检查的代理输出识别 ============================

		[Fact]
		public void LooksLikeGitProxyVersionOutput_DetectsGitVersionForms()
		{
			// 代理转发的版本输出（真 git 的形态）→ true
			Assert.True(GitAiVersionChecker.LooksLikeGitProxyVersionOutput("git version 2.50.1"));
			Assert.True(GitAiVersionChecker.LooksLikeGitProxyVersionOutput("  git version 2.34.1"));
			Assert.True(GitAiVersionChecker.LooksLikeGitProxyVersionOutput("GIT VERSION 2.50.1"));
			// git-ai 原生版本输出形态 → false（"git-ai version" 以 "git-" 开头，不误命中 "git version"）
			Assert.False(GitAiVersionChecker.LooksLikeGitProxyVersionOutput("1.7.2"));
			Assert.False(GitAiVersionChecker.LooksLikeGitProxyVersionOutput("git-ai version 1.7.0"));
			Assert.False(GitAiVersionChecker.LooksLikeGitProxyVersionOutput("git-ai 1.7.0"));
			// null 安全
			Assert.False(GitAiVersionChecker.LooksLikeGitProxyVersionOutput(null));
		}

		[Fact]
		public void GetVersion_ProxyModeFakeBinary_ReturnsNullNotFalseVersion()
		{
			// fake 二进制是 sh 脚本，仅 Unix 可执行（CI 为 ubuntu，与 GitMmSubcommandPathTests 同口径）
			if (OperatingSystem.IsWindows())
			{
				return;
			}
			string root = CreateTempRoot();
			try
			{
				// 修复前：代理输出 "git version 2.50.1" 被解析为 2.50.1 ≥ 1.0.0 → 偏好设置显示
				// 假版本、检查通过，掩盖"文件名非标准导致 stats 不可用"的真实配置问题
				string proxyFake = WriteFakeFile(root, "git-ai-linux-x64", "#!/bin/sh\necho \"git version 2.50.1\"\n");
				MakeExecutable(proxyFake);
				Assert.Null(GitAiVersionChecker.GetVersion(proxyFake));
			}
			finally
			{
				TryDelete(root);
			}
		}

		[Fact]
		public void GetVersion_NativeFakeBinary_ParsesVersion()
		{
			if (OperatingSystem.IsWindows())
			{
				return;
			}
			string root = CreateTempRoot();
			try
			{
				// 正常路径不回归：原生 --version 输出纯版本号 → 正常解析（1.7.2 ≥ 1.0.0 → Ok）
				string nativeFake = WriteFakeFile(root, App.GitAiExecutableName, "#!/bin/sh\necho \"1.7.2\"\n");
				MakeExecutable(nativeFake);
				Assert.Equal(new Version(1, 7, 2), GitAiVersionChecker.GetVersion(nativeFake));
			}
			finally
			{
				TryDelete(root);
			}
		}

		// ============================ 4) stats 命令的代理症状识别 ============================

		[Fact]
		public void IsGitProxyForwardingSymptom_DetectsGitCommandError()
		{
			// 用户截图原文：git: 'stats' is not a git command. See 'git --help'.（附 most similar
			// command 建议）→ 识别为代理转发症状，错误信息翻译成可定位的提示
			Assert.True(GetGitAiStatsGitCommand.IsGitProxyForwardingSymptom("git: 'stats' is not a git command. See 'git --help'."));
			// 完整多行 stderr（含 most similar command 建议行）仍以症状行为准命中
			Assert.True(GetGitAiStatsGitCommand.IsGitProxyForwardingSymptom("git: 'stats' is not a git command. See 'git --help'.\n\nThe most similar command is\n\tstatus\n"));
			Assert.True(GetGitAiStatsGitCommand.IsGitProxyForwardingSymptom("git: 'diff' is not a git command."));
			// 其他失败（超时/权限/仓库异常）不误判
			Assert.False(GetGitAiStatsGitCommand.IsGitProxyForwardingSymptom("fatal: not a git repository"));
			Assert.False(GetGitAiStatsGitCommand.IsGitProxyForwardingSymptom("timed out after 60000ms"));
			Assert.False(GetGitAiStatsGitCommand.IsGitProxyForwardingSymptom(""));
			Assert.False(GetGitAiStatsGitCommand.IsGitProxyForwardingSymptom(null));
		}

		// ============================ 5) 端到端：argv[0] 分发经 staging 链接矫正 ============================

		[Fact]
		public void StagedLink_ExecutionSeesStandardArgvZero()
		{
			// fake 二进制是 sh 脚本，仅 Unix 可执行（CI 为 ubuntu）
			if (OperatingSystem.IsWindows())
			{
				return;
			}
			string root = CreateTempRoot();
			try
			{
				// fake 二进制复刻 git-ai 的 argv[0] 分发：basename 为 git-ai 走原生命令分支，
				// 否则输出真 git 的版本串（代理模式铁证形态）
				string dispatchFake = WriteFakeFile(root, "git-ai-linux-x64",
					"#!/bin/sh\nif [ \"$(basename \"$0\")\" = \"git-ai\" ]; then echo \"git-ai native branch\"; else echo \"git version 2.50.1\"; fi\n");
				MakeExecutable(dispatchFake);

				// 修复前形态：同一二进制以非标准名直跑 → 代理分支（git version 输出）
				GitRequestResult direct = new ShellRequest(root, dispatchFake, new string[1] { "--version" }).Execute();
				Assert.True(direct.Success, "fake 二进制应可执行。ExitCode=" + direct.ExitCode + " Stderr: " + direct.Stderr);
				Assert.Contains("git version", direct.Stdout);

				// 修复后形态：经 staging 链接跑 → argv[0] basename = git-ai → 原生命令分支
				string stagingDir = Path.Combine(root, "staging");
				string staged = App.EnsureGitAiExecutionPath(dispatchFake, stagingDir);
				Assert.NotEqual(dispatchFake, staged);
				GitRequestResult viaLink = new ShellRequest(root, staged, new string[2] { "stats", "HEAD" }).Execute();
				Assert.True(viaLink.Success, "staging 链接应可执行。ExitCode=" + viaLink.ExitCode + " Stderr: " + viaLink.Stderr);
				Assert.Contains("git-ai native branch", viaLink.Stdout);
			}
			finally
			{
				TryDelete(root);
			}
		}

		// ============================ helpers ============================

		private static string CreateTempRoot()
		{
			string root = Path.Combine(Path.GetTempPath(), "fpgitaipath_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(root);
			return root;
		}

		private static string WriteFakeFile(string root, string fileName, string content)
		{
			string path = Path.Combine(root, fileName);
			File.WriteAllText(path, content);
			return path;
		}

		/// <summary>Unix 上给脚本加可执行位（fake 二进制共用）；Windows 无操作。</summary>
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
