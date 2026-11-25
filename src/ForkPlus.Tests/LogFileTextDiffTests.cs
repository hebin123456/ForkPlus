// v5.6.1：.log 强制文本 diff 回归测试。
// .log 本质是文本日志，但程序写入时偶尔夹带 NUL 字节，git 会据此判为二进制
// （输出 "Binary files ... differ"）→ ForkPlus 走 Hex 视图、丢失「部分暂存」能力。
// 本用例构造含 NUL 的 .log，验证 GetWorkingDirectoryFileChangesGitCommand 加 --text 后
// diff 被解析为 Diff.FileType.Text（而非 Binary），从而保留文本编辑器与选区暂存链路。
// 纯命令层验证，不依赖 Avalonia UI，避免 E2E 截图/渲染偶发不稳定。
using System;
using System.IO;
using System.Linq;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Diff;
using Xunit;

namespace ForkPlus.Tests
{
	public class LogFileTextDiffTests
	{
		private static void Run(string exe, string args, string cwd)
		{
			var psi = new System.Diagnostics.ProcessStartInfo(exe, args)
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
				throw new Exception(exe + " " + args + " 失败: " + err);
			}
		}

		/// <summary>构造含 NUL 字节的 .log：git 默认判为二进制，必须靠 --text 才走文本 diff。</summary>
		private static string CreateLogTestRepo()
		{
			string root = Path.Combine(Path.GetTempPath(), "fplogtext_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(root);
			Run("git", "init -q", root);
			Run("git", "config user.email test@example.com", root);
			Run("git", "config user.name Test", root);
			string logPath = Path.Combine(root, "app.log");
			// 文本日志夹带 NUL（0x00），这是 git 判二进制的触发条件
			File.WriteAllBytes(logPath, new byte[] { 0x32, 0x30, 0x32, 0x36, 0x00, 0x2d, 0x20, 0x6f, 0x6b, 0x0a });
			Run("git", "add app.log", root);
			Run("git", "commit -q -m base", root);
			// 修改日志内容（仍含 NUL）
			File.WriteAllBytes(logPath, new byte[] { 0x32, 0x30, 0x32, 0x36, 0x00, 0x2d, 0x20, 0x68, 0x69, 0x0a });
			return root;
		}

		[Fact]
		public void LogFileWithNulBytes_WorkingDirDiff_IsText()
		{
			string repoRoot = CreateLogTestRepo();
			try
			{
				var module = new GitModule(repoRoot, Path.Combine(repoRoot, ".git"), null, null);
				GitCommandResult<ChangedFilesCollection> statusResult = new GetChangedFilesGitCommand().Execute(module);
				Assert.True(statusResult.Succeeded, "git status 失败: " + statusResult.Error);
				ChangedFile logFile = statusResult.Result.ChangedFiles.FirstOrDefault(f => f.Path.EndsWith(".log", StringComparison.OrdinalIgnoreCase));
				Assert.NotNull(logFile);

				GitCommandResult<DiffContent> diffResult = new GetWorkingDirectoryFileChangesGitCommand().Execute(
					module, logFile, null, 3, 4, false, false, false, resolvedConflict: false);
				Assert.True(diffResult.Succeeded, "diff 加载失败: " + diffResult.Error);
				var parsed = diffResult.Result as ParsedDiffContent;
				Assert.NotNull(parsed);
				Assert.NotNull(parsed.Diff);
				Assert.Equal(Diff.FileType.Text, parsed.Diff.Type);
			}
			finally
			{
				try { Directory.Delete(repoRoot, true); } catch { }
			}
		}

		[Fact]
		public void BinaryFileWithNulBytes_StillBinary()
		{
			// 对照：非 .log 的二进制文件（含 NUL）仍应判为 Binary，确保 --text 只针对语义文本扩展名。
			string root = Path.Combine(Path.GetTempPath(), "fpbintext_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(root);
			try
			{
				Run("git", "init -q", root);
				Run("git", "config user.email test@example.com", root);
				Run("git", "config user.name Test", root);
				string binPath = Path.Combine(root, "data.bin");
				File.WriteAllBytes(binPath, new byte[] { 0x00, 0x01, 0x02, 0x03, 0x0a });
				Run("git", "add data.bin", root);
				Run("git", "commit -q -m base", root);
				File.WriteAllBytes(binPath, new byte[] { 0x00, 0x09, 0x08, 0x07, 0x0a });

				var module = new GitModule(root, Path.Combine(root, ".git"), null, null);
				GitCommandResult<ChangedFilesCollection> statusResult = new GetChangedFilesGitCommand().Execute(module);
				Assert.True(statusResult.Succeeded, "git status 失败: " + statusResult.Error);
				ChangedFile binFile = statusResult.Result.ChangedFiles.FirstOrDefault(f => f.Path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase));
				Assert.NotNull(binFile);
				GitCommandResult<DiffContent> diffResult = new GetWorkingDirectoryFileChangesGitCommand().Execute(
					module, binFile, null, 3, 4, false, false, false, resolvedConflict: false);
				Assert.True(diffResult.Succeeded, "diff 加载失败: " + diffResult.Error);
				var parsed = diffResult.Result as ParsedDiffContent;
				Assert.NotNull(parsed);
				Assert.NotNull(parsed.Diff);
				Assert.Equal(Diff.FileType.Binary, parsed.Diff.Type);
			}
			finally
			{
				try { Directory.Delete(root, true); } catch { }
			}
		}
	}
}
