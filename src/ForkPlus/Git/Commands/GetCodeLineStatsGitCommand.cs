using System;
using System.Diagnostics;
using System.IO;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// 代码行数统计命令。spawn tokei.exe 扫描仓库得到按语言聚合的 code/comments/blanks。
	///
	/// 两种模式：
	/// - snapshot（refSpec 为 null/空）：直接在工作区目录跑 tokei，统计当前工作区文件。
	/// - 历史 commit/分支（refSpec 非空）：用 git archive 把 ref 导出成 tar 流到临时目录解压，
	///   再跑 tokei，避免污染工作区。tar 解压走 git 自带 tar（git archive --format=tar）。
	/// </summary>
	public class GetCodeLineStatsGitCommand
	{
		/// <summary>tokei.exe 文件名（与 ForkPlus.exe 同目录，由构建期 RestoreTokei 拉取）。</summary>
		private const string TokeiExeName = "tokei.exe";

		/// <summary>单次 tokei 进程执行的超时（毫秒）。大仓库也基本在 10s 内完成。</summary>
		private const int TimeoutMs = 60_000;

		/// <summary>查找 tokei.exe 路径。优先 ForkPlus.exe 同目录（构建期拉取的副本），
		/// 再退化到 PATH。</summary>
		[Null]
		private static string ResolveTokeiPath()
		{
			try
			{
				string bundled = Path.Combine(App.InstanceDirectory, TokeiExeName);
				if (File.Exists(bundled))
				{
					return bundled;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to resolve bundled tokei.exe path", ex);
			}
			return null;
		}

		/// <summary>统计当前工作区或指定 ref 的代码行数。</summary>
		/// <param name="gitModule">仓库模块。</param>
		/// <param name="refSpec">目标 ref。null/空表示工作区 snapshot；否则是分支名/tag/sha。</param>
		/// <param name="monitor">可选的 JobMonitor（用于进度/取消）。</param>
		public GitCommandResult<CodeLineStats> Execute(GitModule gitModule, [Null] string refSpec, [Null] JobMonitor monitor = null)
		{
			string tokeiExe = ResolveTokeiPath();
			if (tokeiExe == null || !File.Exists(tokeiExe))
			{
				return GitCommandResult<CodeLineStats>.Failure(new GitCommandError.GenericError("tokei.exe not found (expected next to ForkPlus.exe). Code line statistics unavailable."));
			}

			if (monitor != null && monitor.IsCanceled)
			{
				return GitCommandResult<CodeLineStats>.Failure(new GitCommandError.GenericError("canceled"));
			}

			if (string.IsNullOrEmpty(refSpec))
			{
				// snapshot 模式：直接在工作区目录跑 tokei
				monitor?.Update(0, "Running tokei on workspace...");
				return RunTokei(tokeiExe, gitModule.Path, null, refSpec);
			}

			// 历史 ref 模式：git archive <refSpec> 导出 tar 到临时目录，跑 tokei，最后清理
			monitor?.Update(0, "Exporting '" + refSpec + "' via git archive...");
			string tempDir = null;
			try
			{
				tempDir = CreateTempDirectory();
				if (!ExportRefToTarAndExtract(gitModule, refSpec, tempDir, monitor))
				{
					return GitCommandResult<CodeLineStats>.Failure(new GitCommandError.GenericError("git archive failed for ref: " + refSpec));
				}
				if (monitor != null && monitor.IsCanceled)
				{
					return GitCommandResult<CodeLineStats>.Failure(new GitCommandError.GenericError("canceled"));
				}
				monitor?.Update(0.5, "Running tokei on '" + refSpec + "'...");
				return RunTokei(tokeiExe, tempDir, null, refSpec);
			}
			finally
			{
				if (tempDir != null)
				{
					TryDeleteDirectory(tempDir);
				}
			}
		}

		/// <summary>spawn tokei.exe --output json --path &lt;dir&gt;，解析 JSON 返回。</summary>
		private GitCommandResult<CodeLineStats> RunTokei(string tokeiExe, string workingDir, [Null] byte[] stdin, [Null] string refSpec)
		{
			Process process = new Process();
			try
			{
				process.StartInfo = new ProcessStartInfo
				{
					FileName = tokeiExe,
					Arguments = "--output json",
					WorkingDirectory = workingDir,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true,
					StandardOutputEncoding = System.Text.Encoding.UTF8
				};
				process.Start();
				// 异步读 stderr 防止管道满死锁
				string stderr = "";
				var stderrTask = System.Threading.Tasks.Task.Run(() => process.StandardError.ReadToEnd());
				// tokei 不读 stdin
				string stdout = process.StandardOutput.ReadToEnd();
				bool exited = process.WaitForExit(TimeoutMs);
				if (!exited)
				{
					try { process.Kill(); } catch { }
					return GitCommandResult<CodeLineStats>.Failure(new GitCommandError.GenericError("tokei timed out after " + (TimeoutMs / 1000) + "s"));
				}
				stderrTask.Wait();
				stderr = stderrTask.Result;
				if (process.ExitCode != 0)
				{
					Log.Warn("tokei exited with code " + process.ExitCode + ": " + stderr);
					// tokei 对空目录返回 0 但输出 {} 或无输出；非零退出通常是参数错误或仓库异常
					return GitCommandResult<CodeLineStats>.Failure(new GitCommandError.GenericError("tokei failed (exit " + process.ExitCode + "): " + stderr));
				}
				CodeLineStats stats = CodeLineStats.FromTokeiJson(refSpec, stdout);
				return GitCommandResult<CodeLineStats>.Success(stats);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to run tokei", ex);
				return GitCommandResult<CodeLineStats>.Failure(ex);
			}
			finally
			{
				process?.Dispose();
			}
		}

		/// <summary>git archive --format=tar --prefix= &lt;refSpec&gt; 输出到 stdout，
		/// 管道给 tar 解压到 tempDir。Windows 上 git 自带 tar（git-for-windows 内置 bsdtar），
		/// 但更稳妥是用 .NET 4.5 不带原生 tar。这里改用 powershell 的 tar.exe（Windows 10+ 自带）。
		/// 简单稳妥的做法：git archive --format=tar -o &lt;tempFile&gt; &lt;refSpec&gt; 然后
		/// tar -xf &lt;tempFile&gt; -C &lt;tempDir&gt;。</summary>
		private bool ExportRefToTarAndExtract(GitModule gitModule, string refSpec, string tempDir, [Null] JobMonitor monitor)
		{
			string tarFile = Path.Combine(tempDir, "..", "tokei_export_" + Guid.NewGuid().ToString("N") + ".tar");
			tarFile = Path.GetFullPath(tarFile);
			try
			{
				// 1. git archive --format=tar -o <tarFile> <refSpec>
				GitRequestResult archiveResult = new GitRequest(gitModule).Command("archive", "--format=tar", "-o", tarFile, refSpec).Execute(monitor, silent: true);
				if (!archiveResult.Success || archiveResult.ExitCode != 0)
				{
					Log.Warn("git archive failed: " + archiveResult.Stderr);
					return false;
				}
				if (monitor != null && monitor.IsCanceled)
				{
					return false;
				}

				// 2. tar -xf <tarFile> -C <tempDir>
				// Windows 10 1803+ 自带 tar.exe（bsdtar）；CI 的 windows-latest 也有。
				// 在 PATH 找不到 tar.exe 时退化用 powershell Expand-Archive（要求 .zip 格式，
				// 所以这种退化路径不可行）。这里直接调 tar.exe，找不到就报错让用户感知。
				if (!ExtractTar(tarFile, tempDir))
				{
					return false;
				}
				return true;
			}
			finally
			{
				try { if (File.Exists(tarFile)) File.Delete(tarFile); } catch { }
			}
		}

		/// <summary>用系统 tar.exe 解压。Windows 10 1803+ / Windows 11 / CI windows-latest 都自带。</summary>
		private bool ExtractTar(string tarFile, string destDir)
		{
			Process process = new Process();
			try
			{
				process.StartInfo = new ProcessStartInfo
				{
					FileName = "tar.exe",
					Arguments = "-xf \"" + tarFile + "\" -C \"" + destDir + "\"",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				};
				process.Start();
				string stderr = process.StandardError.ReadToEnd();
				process.StandardOutput.ReadToEnd();
				bool exited = process.WaitForExit(TimeoutMs);
				if (!exited)
				{
					try { process.Kill(); } catch { }
					Log.Warn("tar.exe timed out");
					return false;
				}
				if (process.ExitCode != 0)
				{
					Log.Warn("tar.exe failed (exit " + process.ExitCode + "): " + stderr);
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to run tar.exe (Windows 10 1803+ required)", ex);
				return false;
			}
			finally
			{
				process?.Dispose();
			}
		}

		private static string CreateTempDirectory()
		{
			string dir = Path.Combine(System.IO.Path.GetTempPath(), "ForkPlus_tokei_" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(dir);
			return dir;
		}

		private static void TryDeleteDirectory(string dir)
		{
			try
			{
				if (Directory.Exists(dir))
				{
					Directory.Delete(dir, recursive: true);
				}
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to clean temp tokei dir: " + dir, ex);
			}
		}
	}
}
