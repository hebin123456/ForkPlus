using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

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
			[SupportedOSPlatform("windows")]
			public static extern bool SetConsoleCtrlHandler(IntPtr HandlerRoutine, bool Add);

			[DllImport("kernel32.dll", SetLastError = true)]
			[SupportedOSPlatform("windows")]
			public static extern bool AttachConsole(int dwProcessId);

			[DllImport("kernel32.dll", SetLastError = true)]
			[return: MarshalAs(UnmanagedType.Bool)]
			[SupportedOSPlatform("windows")]
			public static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, int dwProcessGroupId);

			[DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
			[SupportedOSPlatform("windows")]
			internal static extern bool FreeConsole();
		}

		// 阶段 5：Unix 信号常量（避免引入 Mono.Posix / System.Runtime.InteropServices.RuntimeInformation 的扩展）。
		// kill(2) 的 SIGINT = 2，定义见 <bits/signum-generic.h>。
		private const int SIGINT = 2;

		[DllImport("libc", SetLastError = true)]
		[SupportedOSPlatform("linux")]
		[SupportedOSPlatform("macos")]
		private static extern int kill(int pid, int signal);

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
			try
			{
				Benchmarker benchmarker = new Benchmarker($"Closing process {id}");
				Log.Info($"Closing process {id}");

				// 阶段 5：跨平台分支。
				// Windows: AttachConsole + GenerateConsoleCtrlEvent(CTRL_C_EVENT) 给同控制台组发 Ctrl+C
				// Unix:    kill(pid, SIGINT) 直接给目标进程发 SIGINT（语义等价 Ctrl+C）
				bool ok;
				if (OperatingSystem.IsWindows())
				{
					ok = SendSigintWindows(process, id);
				}
				else
				{
					ok = SendSigintUnix(id);
					if (ok)
					{
						process.WaitForExit(2000);
					}
				}

				benchmarker.ReportElapsed();
				if (ok)
				{
					Log.Info("Process terminated");
				}
				else
				{
					Log.Info("Process terminating failed");
				}
				return ok;
			}
			catch (Exception ex2)
			{
				Log.Error("Failed to send SIGINT to process", ex2);
			}
			return false;
		}

		[SupportedOSPlatform("windows")]
		private static bool SendSigintWindows(Process process, int id)
		{
			if (!NativeMethods.AttachConsole(id))
			{
				return false;
			}
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
			return true;
		}

		[SupportedOSPlatform("linux")]
		[SupportedOSPlatform("macos")]
		private static bool SendSigintUnix(int id)
		{
			// kill 返回 0 表示成功，-1 表示失败（errno 通过 errno 获取）
			return kill(id, SIGINT) == 0;
		}
	}
}
