using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ForkPlus
{
	public static class ProcessExtensions
	{
		private enum CtrlTypes : uint
		{
			CTRL_C_EVENT = 0u,
			CTRL_BREAK_EVENT = 1u,
			CTRL_CLOSE_EVENT = 2u,
			CTRL_LOGOFF_EVENT = 5u,
			CTRL_SHUTDOWN_EVENT = 6u
		}

		private static class NativeMethods
		{
			[DllImport("kernel32.dll")]
			public static extern bool SetConsoleCtrlHandler(IntPtr HandlerRoutine, bool Add);

			[DllImport("kernel32.dll", SetLastError = true)]
			public static extern bool AttachConsole(int dwProcessId);

			[DllImport("kernel32.dll", SetLastError = true)]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, int dwProcessGroupId);

			[DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
			internal static extern bool FreeConsole();

			/// <summary>
			/// Unix 信号发送（2026-09-07 .exe/Win32 硬编码专项审计补齐）。实证（Linux 沙盒
			/// net10.0）：DllImport("libc") 可解析（运行时按 name→lib{name}.so→{name}.so
			/// 候选链命中已装载的 libc），macOS 同名可用。kill(2) 与 Ctrl+C 等价。
			/// </summary>
			[DllImport("libc", SetLastError = true)]
			internal static extern int kill(int pid, int signal);

			public const int UnixSigint = 2;
		}

		public static bool SendSigintSignal(this Process process)
		{
			int id;
			try
			{
				id = process.Id;
			}
			catch
			{
				return false;
			}
			// Migration note：原版只有 Windows 的 AttachConsole+GenerateConsoleCtrlEvent——
			// Unix 上 AttachConsole 抛 DllNotFoundException 被最外层 catch 吞掉返回 false，
			// 表现为"取消按钮点了 git 进程还在跑"（JobMonitor.Cancel 无 Kill 兜底）。
			// Unix 用 kill(pid, SIGINT) 等价实现：git 收到 SIGINT 会自己收尾（gc 锁、
			// 后台进程正常退出），语义与 Windows 的 Ctrl+C 事件一致。
			if (!OperatingSystem.IsWindows())
			{
				try
				{
					if (NativeMethods.kill(id, NativeMethods.UnixSigint) != 0)
					{
						return false;
					}
					process.WaitForExit(2000);
					Log.Info($"Process {id} interrupted via SIGINT");
					return true;
				}
				catch (Exception ex)
				{
					Log.Error("Failed to send SIGINT event to process " + id, ex);
					return false;
				}
			}
			try
			{
				Benchmarker benchmarker = new Benchmarker($"Closing process {id}");
				Log.Info($"Closing process {id}");
				if (NativeMethods.AttachConsole(id))
				{
					NativeMethods.SetConsoleCtrlHandler(IntPtr.Zero, Add: true);
					try
					{
						if (!NativeMethods.GenerateConsoleCtrlEvent(0u, 0))
						{
							return false;
						}
						process.WaitForExit(2000);
					}
					catch (Exception ex)
					{
						Log.Error("Failed to send SIGNINT event", ex);
					}
					finally
					{
						NativeMethods.FreeConsole();
						NativeMethods.SetConsoleCtrlHandler(IntPtr.Zero, Add: false);
					}
					benchmarker.ReportElapsed();
					Log.Info("Process terminated");
					return true;
				}
				benchmarker.ReportElapsed();
				Log.Info("Process terminating failed");
			}
			catch (Exception ex2)
			{
				Log.Error("Failed to attach to AttachConsole", ex2);
			}
			return false;
		}
	}
}
