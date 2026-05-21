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
