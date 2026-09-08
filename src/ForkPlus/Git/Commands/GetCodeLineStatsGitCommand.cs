using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// 代码行数统计命令。spawn tokei（Windows: tokei.exe / Unix: tokei）扫描仓库得到按语言聚合的 code/comments/blanks。
	///
	/// 两种模式：
	/// - snapshot（refSpec 为 null/空，或 refSpec 指向当前工作区 HEAD）：直接在工作区目录跑 tokei。
	/// - 历史 commit/分支（refSpec 非空且非当前 HEAD）：用 git archive --format=zip 把 ref 导出到
	///   临时目录，用 .NET ZipFile.ExtractToDirectory 原生解压，再跑 tokei，避免污染工作区。
	/// </summary>
	public class GetCodeLineStatsGitCommand
	{
		/// <summary>tokei 可执行文件名（与 ForkPlus 同目录，由构建期 RestoreTokei 拉取）。
		/// Migration note：原版硬编码 "tokei.exe"（Windows-only）。tokei 上游按平台发布产物：
		/// Windows 是 tokei.exe，Linux/macOS 是无扩展名的 tokei——Unix 下找 "tokei.exe" 恒落空，
		/// 统计页在三平台构建里只有 Windows 能用。按当前平台取名。</summary>
		private static readonly string TokeiExeName = OperatingSystem.IsWindows() ? "tokei.exe" : "tokei";

		/// <summary>单次 tokei 进程执行的超时（毫秒）。大仓库也基本在 10s 内完成。</summary>
		private const int TimeoutMs = 60_000;

		/// <summary>查找 tokei 路径。优先 ForkPlus 同目录（构建期拉取的副本），
		/// 再退化到 PATH（用户系统自装的 tokei，如 cargo install / 系统包管理器）。</summary>
		[Null]
		private static string ResolveTokeiPath()
		{
			return ResolveTokeiPath(App.InstanceDirectory, Environment.GetEnvironmentVariable("PATH"));
		}

		/// <summary>可测重载：instanceDirectory 为 bundled 查找目录（ForkPlus 可执行文件所在目录），
		/// pathEnv 为 PATH 环境变量原始内容（参数注入而非读进程环境，测试无需污染全局）。
		/// Migration note（2026-09-04，"linux版本提示 tokei not found"）：注释一直声称
		/// "再退化到 PATH"但实现从未做——bundled 缺失（旧产物/自建时下载失败）时即使系统
		/// 装了 tokei 也直接报 not found。本轮真正实现 PATH 扫描回退。</summary>
		[Null]
		internal static string ResolveTokeiPath(string instanceDirectory, [Null] string pathEnv)
		{
			try
			{
				string bundled = Path.Combine(instanceDirectory, TokeiExeName);
				if (File.Exists(bundled))
				{
					return bundled;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to resolve bundled tokei path", ex);
			}
			// PATH 回退：逐目录探测 &lt;dir&gt;/tokei(.exe)。Unix 上要求可执行位——
			// PATH 里无执行位的同名文件（如解压丢了权限位）spawn 会 Permission denied，
			// 跳过继续找下一个目录；Windows 无执行位概念，存在即可。
			if (!string.IsNullOrEmpty(pathEnv))
			{
				foreach (string rawDir in pathEnv.Split(Path.PathSeparator))
				{
					string dir = rawDir?.Trim();
					if (string.IsNullOrEmpty(dir))
					{
						continue;
					}
					try
					{
						string candidate = Path.Combine(dir, TokeiExeName);
						if (File.Exists(candidate) && IsExecutableFile(candidate))
						{
							return candidate;
						}
					}
					catch (Exception ex)
					{
						Log.Warn("Failed to probe tokei candidate in PATH dir " + dir, ex);
					}
				}
			}
			return null;
		}

		/// <summary>PATH 候选文件的可执行性检查（bundled 副本不走此检查——
		/// EnsureExecutableBit 会补权限位，PATH 上的是系统装的，不该也没权限去改）。</summary>
		private static bool IsExecutableFile(string path)
		{
			if (OperatingSystem.IsWindows())
			{
				return true;
			}
			try
			{
				return (File.GetUnixFileMode(path) & System.IO.UnixFileMode.UserExecute) != 0;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>Unix 下确保 tokei 有可执行位（best-effort）。
		/// Migration note：MSBuild Copy / tar 打包 / 用户手动解压任何一环都可能丢权限位，
		/// spawn 无执行位的文件会报 Permission denied。Windows 无此概念，直接跳过。
		/// 失败只记日志不阻断——spawn 自身失败时会有更具体的错误返回 UI。</summary>
		private static void EnsureExecutableBit(string tokeiPath)
		{
			if (OperatingSystem.IsWindows())
			{
				return;
			}
			try
			{
				System.IO.UnixFileMode mode = File.GetUnixFileMode(tokeiPath);
				System.IO.UnixFileMode wanted = mode | System.IO.UnixFileMode.UserExecute | System.IO.UnixFileMode.GroupExecute | System.IO.UnixFileMode.OtherExecute;
				if (wanted != mode)
				{
					File.SetUnixFileMode(tokeiPath, wanted);
				}
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to ensure execute bit on " + tokeiPath, ex);
			}
		}

		/// <summary>统计当前工作区或指定 ref 的代码行数。</summary>
		/// <param name="gitModule">仓库模块。</param>
		/// <param name="refSpec">目标 ref。null/空表示工作区 snapshot；否则是分支名/tag/sha。</param>
		/// <param name="monitor">可选的 JobMonitor（用于进度/取消）。</param>
		/// <param name="excludePatterns">临时排除的 glob 模式列表（透传给 tokei -e，语义同 .gitignore）。
		/// null 或空则不排除任何内容。不持久化，由 UI 调用方按需传入。</param>
		public GitCommandResult<CodeLineStats> Execute(GitModule gitModule, [Null] string refSpec, [Null] JobMonitor monitor = null, [Null] IList<string> excludePatterns = null)
		{
			string tokeiExe = ResolveTokeiPath();
			if (tokeiExe == null || !File.Exists(tokeiExe))
			{
				// 2026-09-04（"linux版本提示 tokei not found"）：消息带上实际查找位置与自救
				// 指引——旧产物/自建下载失败时 bundled 缺失，用户按提示放一份 tokei 或装到
				// PATH（本轮起 PATH 副本可用）即可恢复，不必重下整个应用。
				return GitCommandResult<CodeLineStats>.Failure(new GitCommandError.GenericError(
					"tokei not found. Searched next to the ForkPlus executable ("
					+ (App.InstanceDirectory ?? "(unknown)")
					+ ") and all PATH directories. Fix: download tokei for your platform from "
					+ "https://github.com/XAMPPRocky/tokei/releases and place '" + TokeiExeName
					+ "' next to the ForkPlus executable, or install tokei on PATH. "
					+ "Code line statistics unavailable."));
			}
			EnsureExecutableBit(tokeiExe);

			if (monitor != null && monitor.IsCanceled)
			{
				return GitCommandResult<CodeLineStats>.Failure(new GitCommandError.GenericError("canceled"));
			}

			if (string.IsNullOrEmpty(refSpec) || IsCurrentWorktreeRef(gitModule, refSpec))
			{
				// snapshot 模式：直接在工作区目录跑 tokei。
				// 当 refSpec 就是当前工作区检出的分支/tag/sha 时也走此路径——
				// git archive 当前 HEAD 检出的 ref 会失败（worktree 占用），直接统计工作区即可。
				monitor?.Update(0, "Running tokei on workspace...");
				return RunTokei(tokeiExe, gitModule.Path, null, refSpec, excludePatterns);
			}

			// 历史 ref 模式：git archive <refSpec> 导出 tar 到临时目录，跑 tokei，最后清理
			monitor?.Update(0, "Exporting '" + refSpec + "' via git archive...");
			string tempDir = null;
			try
			{
				tempDir = CreateTempDirectory();
				string archiveError;
				if (!ExportRefToTarAndExtract(gitModule, refSpec, tempDir, monitor, out archiveError))
				{
					// 区分 ref 不存在 vs archive 其他错误，给出更友好的提示
					string detail = string.IsNullOrEmpty(archiveError) ? "" : " (" + archiveError.Trim() + ")";
					return GitCommandResult<CodeLineStats>.Failure(new GitCommandError.GenericError(
						"git archive failed for ref: " + refSpec + detail));
				}
				if (monitor != null && monitor.IsCanceled)
				{
					return GitCommandResult<CodeLineStats>.Failure(new GitCommandError.GenericError("canceled"));
				}
				monitor?.Update(0.5, "Running tokei on '" + refSpec + "'...");
				return RunTokei(tokeiExe, tempDir, null, refSpec, excludePatterns);
			}
			finally
			{
				if (tempDir != null)
				{
					TryDeleteDirectory(tempDir);
				}
			}
		}

		/// <summary>spawn tokei.exe --output json，解析 JSON 返回。</summary>
		/// <param name="excludePatterns">透传 tokei -e 的 glob 排除模式；null/空则不追加 -e。</param>
		private GitCommandResult<CodeLineStats> RunTokei(string tokeiExe, string workingDir, [Null] byte[] stdin, [Null] string refSpec, [Null] IList<string> excludePatterns)
		{
			Process process = new Process();
			try
			{
				// 用 ArgumentList 逐个追加参数，避免手动拼接时 glob 里的空格/引号被 shell 解析出错。
				// .NET Core 3+ / .NET 10 支持 ProcessStartInfo.ArgumentList。
				process.StartInfo = new ProcessStartInfo
				{
					FileName = tokeiExe,
					WorkingDirectory = workingDir,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true,
					StandardOutputEncoding = System.Text.Encoding.UTF8
				};
				process.StartInfo.ArgumentList.Add("--output");
				process.StartInfo.ArgumentList.Add("json");
				if (excludePatterns != null)
				{
					foreach (string raw in excludePatterns)
					{
						if (string.IsNullOrWhiteSpace(raw))
						{
							continue;
						}
						string pattern = raw.Trim();
						process.StartInfo.ArgumentList.Add("-e");
						process.StartInfo.ArgumentList.Add(pattern);
					}
				}
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

		/// <summary>判断 refSpec 是否指向当前工作区 HEAD（当前检出的分支/tag/sha）。
		/// git archive 当前 HEAD 检出的 ref 会失败（worktree 占用），此时应直接统计工作区。
		/// 比较：① refSpec == 当前分支名（symbolic-ref --short HEAD）
		///       ② refSpec == HEAD 的完整 sha（rev-parse HEAD），覆盖 detached HEAD + sha 输入。
		///       ③ refSpec == HEAD（字面量）。
		/// 任何 git 调用失败均返回 false（保守起见走 archive 路径，让 archive 自己报错）。</summary>
		private bool IsCurrentWorktreeRef(GitModule gitModule, string refSpec)
		{
			if (string.IsNullOrEmpty(refSpec))
			{
				return true;
			}
			if (refSpec == "HEAD")
			{
				return true;
			}
			try
			{
				// 当前分支名（detached HEAD 时为空）
			// 加 -c core.quotePath=false 让 git 输出原始 UTF-8 字节而非八进制转义，避免中文分支名乱码
			GitRequestResult branchResult = new GitRequest(gitModule)
				.Command("-c", "core.quotePath=false", "symbolic-ref", "--quiet", "--short", "HEAD").Execute(silent: true);
				if (branchResult.Success && branchResult.ExitCode == 0)
				{
					string currentBranch = (branchResult.Stdout ?? "").Trim();
					if (!string.IsNullOrEmpty(currentBranch) &&
						string.Equals(currentBranch, refSpec, StringComparison.Ordinal))
					{
						return true;
					}
				}
				// HEAD 的完整 sha，覆盖 detached HEAD + sha 输入
			GitRequestResult headShaResult = new GitRequest(gitModule)
				.Command("-c", "core.quotePath=false", "rev-parse", "HEAD").Execute(silent: true);
				if (headShaResult.Success && headShaResult.ExitCode == 0)
				{
					string headSha = (headShaResult.Stdout ?? "").Trim();
					if (!string.IsNullOrEmpty(headSha) &&
						string.Equals(headSha, refSpec, StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
					// 也比较 refSpec 解析出的 sha（refSpec 可能是短 sha / refs/heads/x 等）
				GitRequestResult refShaResult = new GitRequest(gitModule)
					.Command("-c", "core.quotePath=false", "rev-parse", refSpec).Execute(silent: true);
					if (refShaResult.Success && refShaResult.ExitCode == 0)
					{
						string refSha = (refShaResult.Stdout ?? "").Trim();
						if (!string.IsNullOrEmpty(refSha) &&
							string.Equals(headSha, refSha, StringComparison.OrdinalIgnoreCase))
						{
							return true;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("IsCurrentWorktreeRef check failed", ex);
			}
			return false;
		}

		/// <summary>git archive --format=zip -o &lt;zipFile&gt; &lt;refSpec&gt; 然后逐条目解压（长路径加 \\?\ 前缀）。
		/// 失败时通过 errorMessage 输出诊断信息（优先用 git rev-parse 区分 ref 不存在 vs 其他错误）。</summary>
		private bool ExportRefToTarAndExtract(GitModule gitModule, string refSpec, string tempDir, [Null] JobMonitor monitor, out string errorMessage)
		{
			errorMessage = null;
			// 用 zip 格式而非 tar，避免依赖系统 tar.exe（Windows bsdtar 对 git archive 生成的 tar
			// 中的 pax_global_header 等条目会报 "Invalid empty pathname: Unknown error"）。
			// 解压用逐条目方式 + \\?\ 长路径前缀，不能用 ZipFile.ExtractToDirectory（.NET Framework
			// 4.7.2 的 ExtractToDirectory 内部走 FileStream，路径超 260 会报 DirectoryNotFoundException）。
			string zipFile = Path.Combine(tempDir, "..", "tokei_export_" + Guid.NewGuid().ToString("N") + ".zip");
			zipFile = Path.GetFullPath(zipFile);
			try
			{
				// 1. git archive --format=zip -o <zipFile> <refSpec>
				// 加 -c core.quotePath=false 让 git 输出原始 UTF-8 字节而非八进制转义，避免中文 ref 名乱码
				GitRequestResult archiveResult = new GitRequest(gitModule).Command("-c", "core.quotePath=false", "archive", "--format=zip", "-o", zipFile, refSpec).Execute(monitor, silent: true);
				if (!archiveResult.Success || archiveResult.ExitCode != 0)
				{
					string stderr = archiveResult.Stderr ?? "";
					Log.Warn("git archive failed: " + stderr);
					// 用 git rev-parse 验证 ref 是否可解析，区分"ref 不存在"和"archive 其他错误"
					// （如 worktree 状态异常、ref 只在远端未本地化等）
					GitRequestResult revParseResult = new GitRequest(gitModule)
					.Command("-c", "core.quotePath=false", "rev-parse", "--verify", refSpec + "^{commit}").Execute(silent: true);
					if (!revParseResult.Success || revParseResult.ExitCode != 0)
					{
						errorMessage = "ref '" + refSpec + "' does not resolve to a commit";
					}
					else
					{
						errorMessage = string.IsNullOrEmpty(stderr) ? "git archive exited " + archiveResult.ExitCode : stderr;
					}
					return false;
				}
				if (monitor != null && monitor.IsCanceled)
				{
					errorMessage = "canceled";
					return false;
				}

				// 2. 逐条目解压 zip，对长路径加 \\?\ 前缀绕过 MAX_PATH(260) 限制。
				// 不能用 ZipFile.ExtractToDirectory：.NET Framework 4.7.2 内部走 FileStream，
				// 仓库内深嵌套文件解压到临时目录时路径易超 260，Win32 返回 ERROR_PATH_NOT_FOUND，
				// .NET 映射成 DirectoryNotFoundException "未能找到路径...的一部分"。
				try
				{
					ExtractZipWithLongPathSupport(zipFile, tempDir);
				}
				catch (Exception ex)
				{
					Log.Error("zip extract failed", ex);
					errorMessage = "zip extract failed: " + ex.Message;
					return false;
				}
				return true;
			}
			finally
			{
				try { if (File.Exists(zipFile)) File.Delete(zipFile); } catch { }
			}
		}

		/// <summary>逐条目解压 zip。对超过 MAX_PATH(260) 的条目加 \\?\ 前缀以支持长路径。
		/// .NET Framework 4.7.2 的 ZipFile.ExtractToDirectory 内部用 FileStream，路径超 260 时
		/// Win32 返回 ERROR_PATH_NOT_FOUND，.NET 映射成 DirectoryNotFoundException
		/// "未能找到路径...的一部分"。\\?\ 前缀要求绝对路径且无相对组件(./..)，git archive 的
		/// 条目均为相对路径，与已规范化的 tempDir 拼接后满足该要求，支持到 ~32767 字符。</summary>
		private static void ExtractZipWithLongPathSupport(string zipFile, string destDir)
		{
			using (var archive = System.IO.Compression.ZipFile.OpenRead(zipFile))
			{
				foreach (var entry in archive.Entries)
				{
					// 跳过目录条目（FullName 以 / 结尾）和空名条目
					if (entry.FullName.EndsWith("/") || string.IsNullOrEmpty(entry.Name))
						continue;

					// zip 内用 / 分隔，统一为 \ 再与 destDir 拼接
					string relativePath = entry.FullName.Replace('/', '\\');
					string destPath = Path.Combine(destDir, relativePath);

					// 路径较长时加 \\?\ 前缀绕过 MAX_PATH 限制（248=目录上限，260=文件上限，取 248 保守）
					string longPath = destPath;
					if (longPath.Length > 248 && !longPath.StartsWith(@"\\?\"))
						longPath = @"\\?\" + longPath;

					// 确保父目录存在（\\?\ 前缀的路径 Directory.CreateDirectory 也支持）
					string parentDir = Path.GetDirectoryName(longPath);
					if (!string.IsNullOrEmpty(parentDir))
						Directory.CreateDirectory(parentDir);

					entry.ExtractToFile(longPath, overwrite: true);
				}
			}
		}

		private static string CreateTempDirectory()
		{
			// 用短名(fpt_ + 12 位 guid)减小临时目录基路径长度，降低解压时超过 MAX_PATH 的概率。
			// 长路径的根本兜底在 ExtractZipWithLongPathSupport 的 \\?\ 前缀。
			string dir = Path.Combine(System.IO.Path.GetTempPath(), "fpt_" + Guid.NewGuid().ToString("N").Substring(0, 12));
			Directory.CreateDirectory(dir);
			return dir;
		}

		private static void TryDeleteDirectory(string dir)
		{
			try
			{
				if (Directory.Exists(dir))
				{
					// 加 \\?\ 前缀以支持删除含长路径文件的目录（解压时可能生成长路径文件）
					string longDir = dir;
					if (longDir.Length > 248 && !longDir.StartsWith(@"\\?\"))
						longDir = @"\\?\" + longDir;
					Directory.Delete(longDir, recursive: true);
				}
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to clean temp tokei dir: " + dir, ex);
			}
		}
	}
}
