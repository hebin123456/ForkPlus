using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Avalonia.Threading;

namespace ForkPlus
{
	/// <summary>
	/// v4.0.6 UI 冻结看门狗：把"界面卡住不动"从黑盒变成带现场的事件记录。
	///
	/// 背景：v4.0.5 已有崩溃兜底（CrashDumper，crash-*.log），但"卡着卡着崩溃"的前半段
	/// ——卡住——完全没有现场：托管异常兜底只能抓崩后的尸体，卡住当下 UI 线程到底在
	/// 执行什么、哪些 git 任务在跑，一概不知。
	///
	/// 设计：独立后台线程（绝不占用 UI 线程/线程池）周期性向 Dispatcher.UIThread 以
	/// SystemIdle 优先级投递心跳；连续超过阈值（默认 6 秒）未收到心跳即判定冻结：
	///   1) 写冻结报告 freeze-*.log（版本/系统/冻结时长/运行中的后台任务/GC 状态），
	///      冻结持续期间每 5 秒追加心跳行，恢复时追加恢复行；
	///   2) 尝试用 .NET 运行时自带的 createdump 对自身进程做一份转储（freeze-*.dmp，
	///      冷却 60 秒，dotnet-dump 可直接看到卡住时 UI 线程的完整托管栈——这正是
	///      "卡"的定位钥匙，纯托管代码无法从其他线程抓取冻结线程的栈，转储是唯一手段）；
	///   3) 全程日志记录，绝不打断/杀死应用。
	///
	/// 时钟用 Environment.TickCount64（单调时钟），系统休眠唤醒不会误报。
	/// 看门狗线程自身所有代码都在 try/catch 内——看门狗绝不能成为新的崩溃/卡顿源。
	/// 环境变量 FORKPLUS_DISABLE_FREEZE_WATCHDOG=1 可整体停用（排障开关）。
	/// </summary>
	public static class UiFreezeWatchdog
	{
		/// <summary>心跳间隔（毫秒）。</summary>
		public const int HeartbeatIntervalMs = 1000;

		/// <summary>连续无心跳多久判定冻结（毫秒）。</summary>
		public const int FreezeThresholdMs = 6000;

		/// <summary>冻结持续期间状态行的追加间隔（毫秒）。</summary>
		private const int StillFrozenAppendIntervalMs = 5000;

		/// <summary>冻结转储的冷却间隔（毫秒）——持续冻结不反复写几百 MB 的转储。</summary>
		private const int DumpCooldownMs = 60000;

		private const int MaxFreezeFiles = 10;

		private static Thread _thread;
		private static CancellationTokenSource _cancellation;
		private static long _lastHeartbeatMs;
		private static long _lastDumpMs = long.MinValue;

		// internal（InternalsVisibleTo）：回归测试专用开关/目录重定向，生产不设。
		private static string _freezeLogDirectoryOverrideForTests;
		private static bool _dumpCreationEnabledForTests = true;

		/// <summary>冻结报告目录（与 crash-*.log、dump-*.dmp 同目录）。</summary>
		public static string FreezeLogDirectory => _freezeLogDirectoryOverrideForTests ?? Path.Combine(App.ForkDirectoryPath, "logs");

		/// <summary>测试专用：临时重定向报告目录。传 null 恢复。finally 中务必复位。</summary>
		internal static string FreezeLogDirectoryOverrideForTests
		{
			get => _freezeLogDirectoryOverrideForTests;
			set => _freezeLogDirectoryOverrideForTests = value;
		}

		/// <summary>测试专用：置 false 后冻结事件不再拉起 createdump（单测里不写几百 MB 转储）。</summary>
		internal static bool DumpCreationEnabledForTests
		{
			get => _dumpCreationEnabledForTests;
			set => _dumpCreationEnabledForTests = value;
		}

		/// <summary>启动看门狗（RunStartup 主窗口显示后调用）。幂等。</summary>
		public static void Start()
		{
			try
			{
				if (Environment.GetEnvironmentVariable("FORKPLUS_DISABLE_FREEZE_WATCHDOG") == "1")
				{
					return;
				}
			}
			catch
			{
			}
			if (_thread != null)
			{
				return;
			}
			Thread thread = new Thread(WatchdogLoop)
			{
				IsBackground = true,
				Name = "ForkPlus.UiFreezeWatchdog",
				Priority = ThreadPriority.BelowNormal
			};
			if (Interlocked.CompareExchange(ref _thread, thread, null) == null)
			{
				Interlocked.Exchange(ref _lastHeartbeatMs, Environment.TickCount64);
			 _cancellation = new CancellationTokenSource();
				thread.Start();
			}
		}

		/// <summary>停止看门狗（RunExit 调用）。幂等。</summary>
		public static void Stop()
		{
			Thread thread = Interlocked.Exchange(ref _thread, null);
			if (thread == null)
			{
				return;
			}
			try
			{
				_cancellation?.Cancel();
			}
			catch
			{
			}
			try
			{
				thread.Join(3000);
			}
			catch
			{
			}
		}

		/// <summary>纯函数：按最近心跳时刻判定当前是否冻结（测试直测）。</summary>
		internal static bool IsFrozen(long lastHeartbeatMs, long nowMs, int thresholdMs)
			=> unchecked(nowMs - lastHeartbeatMs) > thresholdMs;

		private static void WatchdogLoop()
		{
			string currentFreezeLogPath = null;
			long frozenSinceMs = 0;
			long lastAppendMs = 0;
			try
			{
				while (!(_cancellation?.IsCancellationRequested ?? true))
				{
					try
					{
						bool cancellationRequested = _cancellation.Token.WaitHandle.WaitOne(HeartbeatIntervalMs);
						if (cancellationRequested)
						{
							break;
						}
						PostHeartbeat();
						long nowMs = Environment.TickCount64;
						long lastMs = Interlocked.Read(ref _lastHeartbeatMs);
						bool frozen = IsFrozen(lastMs, nowMs, FreezeThresholdMs);
						if (frozen && frozenSinceMs == 0)
						{
							frozenSinceMs = nowMs - FreezeThresholdMs;
							long frozenDurationMs = nowMs - frozenSinceMs;
							currentFreezeLogPath = WriteFreezeReport(frozenDurationMs);
							lastAppendMs = nowMs;
							TryCreateFreezeDump(nowMs);
						}
						else if (frozen)
						{
							if (currentFreezeLogPath != null && unchecked(nowMs - lastAppendMs) >= StillFrozenAppendIntervalMs)
							{
								AppendLine(currentFreezeLogPath, "Still frozen at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
									+ " (+" + ((nowMs - frozenSinceMs) / 1000.0).ToString("F1") + "s)");
								lastAppendMs = nowMs;
							}
						}
						else if (frozenSinceMs != 0)
						{
							if (currentFreezeLogPath != null)
							{
								AppendLine(currentFreezeLogPath, "Recovered at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
									+ " (total " + ((Environment.TickCount64 - frozenSinceMs) / 1000.0).ToString("F1") + "s)");
							}
							frozenSinceMs = 0;
							currentFreezeLogPath = null;
						}
					}
					catch
					{
						// 单次循环失败继续下一轮（看门狗永不退出、永不抛出）。
					}
				}
			}
			catch
			{
			}
		}

		private static void PostHeartbeat()
		{
			long ticks = Environment.TickCount64;
			Dispatcher.UIThread.Post(delegate
			{
				Interlocked.Exchange(ref _lastHeartbeatMs, ticks);
			}, DispatcherPriority.SystemIdle);
		}

		/// <summary>写冻结报告文件（freeze-yyyyMMdd-HHmmss-fff.log），返回文件路径。</summary>
		internal static string WriteFreezeReport(long frozenDurationMs)
		{
			string path = null;
			try
			{
				string directory = FreezeLogDirectory;
				Directory.CreateDirectory(directory);
				path = Path.Combine(directory, "freeze-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".log");
				File.WriteAllText(path, BuildFreezeReport(frozenDurationMs), Encoding.UTF8);
				PruneOldFreezeFiles(directory);
			}
			catch
			{
				path = null;
			}
			try
			{
				Log.Warn("UI freeze detected (" + (frozenDurationMs / 1000.0).ToString("F1") + "s without heartbeat). Report: " + (path ?? "<failed>"));
			}
			catch
			{
			}
			return path;
		}

		/// <summary>构建冻结报告正文（含运行中后台任务快照，测试直测内容断言）。</summary>
		internal static string BuildFreezeReport(long frozenDurationMs)
		{
			StringBuilder builder = new StringBuilder(4096);
			builder.AppendLine("==== ForkPlus UI freeze report ====");
			try
			{
				builder.AppendLine("Time:       " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
				builder.AppendLine("FrozenFor:  " + (frozenDurationMs / 1000.0).ToString("F1") + "s (UI thread unresponsive)");
				builder.AppendLine("Version:    " + App.Version);
				builder.AppendLine("OS:         " + RuntimeInformation.OSDescription + " (" + RuntimeInformation.OSArchitecture + ")");
				builder.AppendLine("Runtime:    " + RuntimeInformation.FrameworkDescription + " " + RuntimeInformation.ProcessArchitecture);
				builder.AppendLine("PID:        " + App.ProcessId);
				builder.AppendLine("Threads:    " + Process.GetCurrentProcess().Threads.Count);
				builder.AppendLine("GC:         gen0=" + GC.CollectionCount(0) + " gen1=" + GC.CollectionCount(1) + " gen2=" + GC.CollectionCount(2)
					+ " heap=" + (GC.GetTotalMemory(forceFullCollection: false) / (1024.0 * 1024.0)).ToString("F1") + "MB");
				builder.AppendLine();
				builder.AppendLine(BuildRunningJobsSection());
			}
			catch (Exception ex)
			{
				builder.AppendLine("<report build failed: " + ex.Message + ">");
			}
			builder.AppendLine();
			builder.AppendLine("==== end of freeze report ====");
			return builder.ToString();
		}

		/// <summary>运行中的后台任务快照（JobQueue 全局注册表聚合，至多 20 条）。</summary>
		private static string BuildRunningJobsSection()
		{
			StringBuilder builder = new StringBuilder(1024);
			builder.AppendLine("---- Running background jobs ----");
			try
			{
				Jobs.Job[] runningJobs = Jobs.JobQueue.GetRunningJobsGlobally();
				if (runningJobs.Length == 0)
				{
					builder.AppendLine("<none>");
				}
				else
				{
					DateTime utcNow = DateTime.UtcNow;
					foreach (Jobs.Job job in runningJobs.Take(20))
					{
						string elapsed = job.StartTime.HasValue
							? ((utcNow - job.StartTime.Value).TotalSeconds.ToString("F1") + "s")
							: "?";
						builder.AppendLine("- " + job.Name + " [" + job.Status + "] elapsed=" + elapsed
							+ " progress=" + (job.Monitor?.ProgressMessage ?? "<none>"));
					}
					if (runningJobs.Length > 20)
					{
						builder.AppendLine("... and " + (runningJobs.Length - 20) + " more");
					}
				}
			}
			catch (Exception ex)
			{
				builder.AppendLine("<job snapshot failed: " + ex.Message + ">");
			}
			return builder.ToString();
		}

		/// <summary>冻结现场转储：拉起运行时自带 createdump 对自身 PID 写 WithHeap 转储。</summary>
		private static void TryCreateFreezeDump(long nowMs)
		{
			try
			{
				if (!_dumpCreationEnabledForTests || Debugger.IsAttached)
				{
					return;
				}
				if (unchecked(nowMs - _lastDumpMs) < DumpCooldownMs)
				{
					return;
				}
				string createdump = FindCreateDumpExecutable();
				if (createdump == null)
				{
					Log.Warn("UI freeze dump skipped: createdump not found next to the runtime.");
					_lastDumpMs = nowMs;
					return;
				}
				_lastDumpMs = nowMs;
				string dumpPath = Path.Combine(FreezeLogDirectory, "freeze-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + App.ProcessId + ".dmp");
				ProcessStartInfo startInfo = new ProcessStartInfo(createdump, "-f \"" + dumpPath + "\" --withheap " + App.ProcessId)
				{
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				};
				// 看门狗线程独立等待，转储写完与否都不影响看门狗继续心跳监测；
				// 超时只是不再等（createdump 继续在后台写完）。
				using (Process process = Process.Start(startInfo))
				{
					if (process != null)
					{
						process.WaitForExit(60000);
					}
				}
				Log.Warn("UI freeze dump written: " + dumpPath);
			}
			catch (Exception ex)
			{
				try
				{
					Log.Warn("UI freeze dump failed: " + ex.Message);
				}
				catch
				{
				}
			}
		}

		/// <summary>createdump 位于运行时目录（GetRuntimeDirectory，self-contained 发布时即应用目录）。</summary>
		private static string FindCreateDumpExecutable()
		{
			try
			{
				string name = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "createdump.exe" : "createdump";
				string candidate = Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), name);
				return File.Exists(candidate) ? candidate : null;
			}
			catch
			{
				return null;
			}
		}

		private static void AppendLine(string path, string line)
		{
			try
			{
				File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
			}
			catch
			{
			}
		}

		private static void PruneOldFreezeFiles(string directory)
		{
			try
			{
				string[] files = Directory.GetFiles(directory, "freeze-*.log");
				foreach (string file in files.OrderByDescending((string x) => x, StringComparer.Ordinal).Skip(MaxFreezeFiles))
				{
					try
					{
						File.Delete(file);
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
		}
	}
}
