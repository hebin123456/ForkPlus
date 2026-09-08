// 回归测试（2026-09-04，"linux版本提示 tokei not found"）：
// 根因链：① 08-30 三平台化之前的旧 Linux 产物不含 tokei（当时构建机制 Windows-only）；
// ② 更关键的是运行时缺陷——ResolveTokeiPath 的注释一直声称"再退化到 PATH"但实现从未做，
//   bundled 缺失时即使系统装了 tokei（cargo install / 包管理器）也直接报 not found。
// 修复：真正实现 PATH 扫描回退（Unix 上要求可执行位）+ 错误信息带查找位置与自救指引。
// 本测试直接调 internal 可测重载（ReflogHistoryProviderTests 同款"internal 供测试"先例）：
// PATH 作为参数注入而非改进程环境变量——xunit 其他 collection 可能并行，污染全局 PATH
// 有竞态风险。假 tokei 用文本文件占位即可（解析只查存在性与权限位，不执行）。
using System;
using System.IO;
using System.Linq;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using Xunit;

namespace ForkPlus.Tests
{
	public class TokeiResolutionTests
	{
		// 与 GetCodeLineStatsGitCommand.TokeiExeName 同规则（按平台取名）
		private static string TokeiFileName
		{
			get { return OperatingSystem.IsWindows() ? "tokei.exe" : "tokei"; }
		}

		// 仓库根：从测试输出目录向上找含 src/ForkPlus.Tests 的目录
		//（EvidenceScreenshotTests.FindRepoRoot 同款）
		private static string FindRepoRoot()
		{
			string dir = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Parent?.Parent?.Parent?.FullName;
			while (dir != null && !Directory.Exists(Path.Combine(dir, "src", "ForkPlus.Tests")))
			{
				dir = Directory.GetParent(dir)?.FullName;
			}
			return dir ?? throw new InvalidOperationException("找不到仓库根（src/ForkPlus.Tests 不存在）");
		}

		private static string RunGit(string args, string cwd)
		{
			var psi = new System.Diagnostics.ProcessStartInfo("git", args)
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				WorkingDirectory = cwd
			};
			using var p = System.Diagnostics.Process.Start(psi);
			string err = p.StandardError.ReadToEnd();
			p.WaitForExit();
			if (p.ExitCode != 0)
			{
				throw new Exception("git " + args + " 失败: " + err);
			}
			return p.StandardOutput.ReadToEnd();
		}

		/// <summary>在 dir 下造一个假 tokei 文件，返回其完整路径。
		/// executable=false 时 Unix 上故意去掉可执行位（模拟解压丢权限位的脏 PATH 副本）。</summary>
		private static string MakeFakeTokei(string dir, bool executable)
		{
			Directory.CreateDirectory(dir);
			string path = Path.Combine(dir, TokeiFileName);
			File.WriteAllText(path, "fake-tokei-for-tests");
			if (!OperatingSystem.IsWindows())
			{
				System.IO.UnixFileMode mode = System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite;
				if (executable)
				{
					mode |= System.IO.UnixFileMode.UserExecute;
				}
				File.SetUnixFileMode(path, mode);
			}
			return path;
		}

		private static string MakeTempRoot()
		{
			return Path.Combine(Path.GetTempPath(), "fpt_tokei_" + Guid.NewGuid().ToString("N"));
		}

		[Fact]
		public void BundledCopy_TakesPrecedence_OverPath()
		{
			string root = MakeTempRoot();
			try
			{
				string appDir = Path.Combine(root, "app");
				string pathDir = Path.Combine(root, "pathbin");
				string bundled = MakeFakeTokei(appDir, executable: true);
				MakeFakeTokei(pathDir, executable: true);

				string resolved = GetCodeLineStatsGitCommand.ResolveTokeiPath(appDir, pathDir);

				Assert.NotNull(resolved);
				Assert.Equal(Path.GetFullPath(bundled), Path.GetFullPath(resolved));
			}
			finally
			{
				Directory.Delete(root, recursive: true);
			}
		}

		[Fact]
		public void MissingBundled_FallsBackToPath()
		{
			string root = MakeTempRoot();
			try
			{
				// appDir 存在但无 tokei；PATH 含一个坏目录 + 一个含可执行 tokei 的目录
				string appDir = Path.Combine(root, "app");
				Directory.CreateDirectory(appDir);
				string goodDir = Path.Combine(root, "goodbin");
				string onPath = MakeFakeTokei(goodDir, executable: true);
				string pathEnv = Path.Combine(root, "nonexistent-dir") + Path.PathSeparator + goodDir;

				string resolved = GetCodeLineStatsGitCommand.ResolveTokeiPath(appDir, pathEnv);

				Assert.NotNull(resolved);
				Assert.Equal(Path.GetFullPath(onPath), Path.GetFullPath(resolved));
			}
			finally
			{
				Directory.Delete(root, recursive: true);
			}
		}

		[Fact]
		public void PathCandidate_WithoutExecuteBit_IsSkipped_OnUnix()
		{
			if (OperatingSystem.IsWindows())
			{
				// Windows 无执行位概念，此行为不适用（软跳过）
				return;
			}
			string root = MakeTempRoot();
			try
			{
				string appDir = Path.Combine(root, "app");
				Directory.CreateDirectory(appDir);
				string noExecDir = Path.Combine(root, "noexec");
				string execDir = Path.Combine(root, "exec");
				MakeFakeTokei(noExecDir, executable: false);
				string good = MakeFakeTokei(execDir, executable: true);

				string resolved = GetCodeLineStatsGitCommand.ResolveTokeiPath(appDir, noExecDir + Path.PathSeparator + execDir);

				// 无执行位的副本必须被跳过，落到后面目录的可执行副本
				Assert.NotNull(resolved);
				Assert.Equal(Path.GetFullPath(good), Path.GetFullPath(resolved));
			}
			finally
			{
				Directory.Delete(root, recursive: true);
			}
		}

		[Fact]
		public void MissingEverywhere_ReturnsNull()
		{
			string root = MakeTempRoot();
			try
			{
				string appDir = Path.Combine(root, "app");
				Directory.CreateDirectory(appDir);

				Assert.Null(GetCodeLineStatsGitCommand.ResolveTokeiPath(appDir, Path.Combine(root, "nonexistent-a") + Path.PathSeparator + Path.Combine(root, "nonexistent-b")));
				// PATH 为空串 / null 也必须安全返回 null（不抛异常）
				Assert.Null(GetCodeLineStatsGitCommand.ResolveTokeiPath(appDir, ""));
				Assert.Null(GetCodeLineStatsGitCommand.ResolveTokeiPath(appDir, null));
			}
			finally
			{
				Directory.Delete(root, recursive: true);
			}
		}

		[Fact]
		public void NullInstanceDirectory_DoesNotThrow()
		{
			// App.InstanceDirectory 极端情况下可能为 null（如单文件发布的空 Location），
			// bundled 段 Path.Combine(null, ...) 的 ArgumentNullException 必须被吞掉（记日志）
			// 并继续 PATH 扫描，而不是炸到 UI。PATH 也一并传 null / 不存在目录验证全链路安全。
			Assert.Null(GetCodeLineStatsGitCommand.ResolveTokeiPath(null, null));
			Assert.Null(GetCodeLineStatsGitCommand.ResolveTokeiPath(null, "/nonexistent-dir-xyz"));
		}

		/// <summary>端到端（2026-09-04，"linux版本提示 tokei not found"）：
		/// bundled 副本缺失（旧产物/自建下载失败的用户现场）+ 系统装了 tokei 的场景，
		/// Execute 全链路应经 PATH 回退用真实 tokei 完成统计。用真实二进制跑真文件，
		/// 断言拿到非零代码行数——证明修复对真实用户现场可自愈，而非仅解析逻辑正确。</summary>
		[Fact]
		public void MissingBundled_RealTokeiOnPath_ExecuteSucceeds_EndToEnd()
		{
			// 前置1：构建期 RestoreTokei 拉取的真实 tokei（third_party/）。离线环境跑测试时
			// 可能不存在——软跳过而非硬失败（解析逻辑已由上面的纯单测覆盖）。
			string repoRoot = FindRepoRoot();
			string realTokei = Path.Combine(repoRoot, "third_party", TokeiFileName);
			if (!File.Exists(realTokei))
			{
				return;
			}
			// 前置2：bundled 位置（测试进程的 ForkPlus.dll 所在目录）必须无 tokei，
			// 否则测的是 bundled 路径而非 PATH 回退（如将来有人给测试工程恢复 tokei 拷贝）。
			if (App.InstanceDirectory != null && File.Exists(Path.Combine(App.InstanceDirectory, TokeiFileName)))
			{
				return;
			}

			string root = MakeTempRoot();
			string oldPath = Environment.GetEnvironmentVariable("PATH");
			try
			{
				// 1) 模拟用户仓库工作区：git init + 两个 C# 文件
				string workDir = Path.Combine(root, "repo");
				Directory.CreateDirectory(workDir);
				RunGit("init -q", workDir);
				File.WriteAllText(Path.Combine(workDir, "a.cs"), "class A { }\n");
				File.WriteAllText(Path.Combine(workDir, "b.cs"), "class B { }\n");

				// 2) PATH 目录放真实 tokei 副本（Unix 带可执行位）
				string pathDir = Path.Combine(root, "pathbin");
				Directory.CreateDirectory(pathDir);
				string tokeiCopy = Path.Combine(pathDir, TokeiFileName);
				File.Copy(realTokei, tokeiCopy, overwrite: true);
				if (!OperatingSystem.IsWindows())
				{
					File.SetUnixFileMode(tokeiCopy,
						System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite | System.IO.UnixFileMode.UserExecute);
				}

				// 3) 追加式改进程 PATH（原 PATH 的超集——并行测试 spawn git 等不受影响）
				Environment.SetEnvironmentVariable("PATH", oldPath + Path.PathSeparator + pathDir);

				// 4) 端到端：Execute → ResolveTokeiPath（bundled 缺失 → PATH 回退）→ 真实 tokei 执行
				var module = new GitModule(workDir, Path.Combine(workDir, ".git"), null, null);
				GitCommandResult<CodeLineStats> result = new GetCodeLineStatsGitCommand().Execute(module, null);

				Assert.True(result.Succeeded, "Execute 失败: " + result.Error);
				Assert.True(result.Result.TotalCode > 0, "应统计到非零代码行");
				LanguageStats csharp = result.Result.Languages.FirstOrDefault(l => l.Name == "C#");
				Assert.NotNull(csharp);
				Assert.True(csharp.Code > 0, "C# 代码行应 > 0，实际 " + csharp.Code);
			}
			finally
			{
				Environment.SetEnvironmentVariable("PATH", oldPath);
				Directory.Delete(root, recursive: true);
			}
		}
	}
}
