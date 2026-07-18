using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace ForkPlus.RI.Tests
{
	/// <summary>
	/// ForkPlus.RI.exe 单元测试。
	///
	/// RI（Rebase Interactive）是 git rebase -x 调用的辅助程序：
	///   1. 读取 FORK_PLUS_PROCESS_ID 环境变量
	///   2. 读取首个命令行参数作为 todo list 文件路径
	///   3. 通过 NamedPipe 把 "prepareTodoListForRebase " + path 发给 ForkPlus 主进程
	///   4. 主进程返回 "start" → exit 0；其他 → exit 1
	///
	/// 测试策略：黑盒进程测试。只覆盖入口检查（无 processId / 无 args 立即返回 1），
	/// 不覆盖 pipe 通信（需要真实 ForkPlus 主进程作为 pipe server，30s 超时太慢）。
	/// </summary>
	public class RebaseInteractiveProgramTests
	{
		private static string ExePath =>
			Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ForkPlus.RI.exe");

		/// <summary>
		/// 启动 ForkPlus.RI.exe，传入参数和环境变量，返回 exit code。
		/// 超时 10 秒（无 processId 或无 args 时应立即返回）。
		/// </summary>
		private static int RunRI(string args, Action<ProcessStartInfo> configureEnv = null)
		{
			var psi = new ProcessStartInfo
			{
				FileName = ExePath,
				Arguments = args ?? "",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			// 关键：清空 FORK_PLUS_PROCESS_ID，避免继承测试进程的环境变量
			psi.EnvironmentVariables["FORK_PLUS_PROCESS_ID"] = "";
			configureEnv?.Invoke(psi);

			using (var proc = Process.Start(psi))
			{
				Assert.NotNull(proc);
				proc.StandardOutput.ReadToEnd();
				proc.StandardError.ReadToEnd();
				if (!proc.WaitForExit(10000))
				{
					proc.Kill();
					proc.WaitForExit(2000);
					throw new TimeoutException("ForkPlus.RI.exe 未在 10 秒内退出。");
				}
				return proc.ExitCode;
			}
		}

		[Fact]
		public void Exe_Exists()
		{
			Assert.True(File.Exists(ExePath), $"ForkPlus.RI.exe 未找到于 {ExePath}。");
		}

		[Fact]
		public void NoProcessId_Returns1()
		{
			// 无 FORK_PLUS_PROCESS_ID → 立即返回 1（即使有 args）
			Assert.Equal(1, RunRI(@"""C:\fake\todo.txt"""));
		}

		[Fact]
		public void EmptyArgs_Returns1()
		{
			// 无参数 → args.Length == 0 → 立即返回 1
			Assert.Equal(1, RunRI(""));
		}

		[Fact]
		public void WhitespaceArg_Returns1()
		{
			// 参数是空白字符串 → IsNullOrWhiteSpace(args[0]) → 返回 1
			// 注意：空字符串作为 Arguments 时 ProcessStartInfo 会传一个空 args[0]
			Assert.Equal(1, RunRI("\" \""));
		}

		[Fact]
		public void NoProcessId_WithValidPath_Returns1()
		{
			// 有合理的 todo 路径但无 processId → 返回 1
			Assert.Equal(1, RunRI(@"""C:\path\to\git-rebase-todo"""));
		}

		[Fact]
		public void NoProcessId_EmptyArgs_Returns1()
		{
			// 无 processId 且无 args → 返回 1（双重保护）
			Assert.Equal(1, RunRI(""));
		}
	}
}
