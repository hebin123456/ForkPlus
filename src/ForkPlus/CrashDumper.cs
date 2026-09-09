using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ForkPlus
{
	/// <summary>
	/// v4.0.5 崩溃兜底日志：独立于 NLog 的崩溃转储写入器。
	///
	/// 背景（"UI 经常无响应崩溃，把崩溃前的日志反应给你"）：
	///   1) 原日志 fork.log 配置了 DeleteOldFileOnStartup=true——崩溃后用户一旦重启应用，
	///      崩溃现场即被删除，永远来不及拷贝（本版本同时改为滚动归档，见
	///      ProductionLoggingConfiguration）；
	///   2) NLog FileTarget 有异步批量写缓冲，进程硬崩（SIGSEGV/AppDomain 异常）时缓冲
	///      尾部可能丢失。
	///
	/// 设计：崩溃事件直接用 File.WriteAllText 落独立文件（logs/crash-时间戳.log，同步写、
	/// 不经 NLog、无缓冲），内容含版本/系统/运行时/异常全链（含 InnerException 与堆栈）。
	/// 保留最近 10 份防止无限增长。本类自身绝不抛异常（兜底不能成为新崩溃源）。
	/// </summary>
	public static class CrashDumper
	{
		private const int MaxCrashFiles = 10;

		// internal（InternalsVisibleTo）：回归测试把转储目录指向临时目录做密封验证——
		// 保留策略断言（"恰好剩 10 份、最旧被清"）要求完全掌控目录内容，真实数据目录里
		// 其他用例异步落下的转储会污染计数。仅测试使用，生产恒为 null。
		private static string _crashLogDirectoryOverrideForTests;

		/// <summary>崩溃转储目录（与 fork.log 同目录，便于一并打包反馈）。</summary>
		public static string CrashLogDirectory => _crashLogDirectoryOverrideForTests ?? Path.Combine(App.ForkDirectoryPath, "logs");

		/// <summary>测试专用：临时重定向转储目录。传 null 恢复。finally 中务必复位。</summary>
		internal static string CrashLogDirectoryOverrideForTests
		{
			get => _crashLogDirectoryOverrideForTests;
			set => _crashLogDirectoryOverrideForTests = value;
		}

		/// <summary>
		/// 写崩溃转储。kind 标识来源（UI / AppDomain-fatal / Task-unobserved 等），
		/// context 为附加上下文（如触发场景描述）。
		/// </summary>
		public static void Dump(string kind, Exception exception, string context = null)
		{
			string path = null;
			try
			{
				string directory = CrashLogDirectory;
				Directory.CreateDirectory(directory);
				path = Path.Combine(directory, "crash-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".log");
				File.WriteAllText(path, BuildReport(kind, exception, context), Encoding.UTF8);
				PruneOldCrashFiles(directory);
			}
			catch
			{
				// 兜底路径自身失败（磁盘满/权限）时静默——不能再抛出新异常。
			}
			try
			{
				// 同步写入常规日志（NLog 需要时 flush 由调用方/退出钩子负责）。
				Log.Error("Crash dump written: " + (path ?? "<failed>") + " [" + kind + "]", exception);
			}
			catch
			{
			}
		}

		/// <summary>尽力冲刷 NLog 缓冲（进程即将终止的场景调用）。</summary>
		public static void FlushLogs()
		{
			try
			{
				NLog.LogManager.Flush(TimeSpan.FromSeconds(2.0));
			}
			catch
			{
			}
		}

		private static string BuildReport(string kind, Exception exception, string context)
		{
			StringBuilder stringBuilder = new StringBuilder(4096);
			stringBuilder.AppendLine("==== ForkPlus crash dump ====");
			try
			{
				stringBuilder.AppendLine("Time:      " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
				stringBuilder.AppendLine("Kind:      " + (kind ?? "<unknown>"));
				stringBuilder.AppendLine("Version:   " + App.Version);
				stringBuilder.AppendLine("OS:        " + RuntimeInformation.OSDescription + " (" + RuntimeInformation.OSArchitecture + ")");
				stringBuilder.AppendLine("Runtime:   " + RuntimeInformation.FrameworkDescription + " " + RuntimeInformation.ProcessArchitecture);
				stringBuilder.AppendLine("DataDir:   " + App.ForkDirectoryPath);
				if (!string.IsNullOrEmpty(context))
				{
					stringBuilder.AppendLine("Context:   " + context);
				}
				stringBuilder.AppendLine();
				stringBuilder.AppendLine("---- Exception ----");
				AppendException(stringBuilder, exception);
			}
			catch (Exception ex)
			{
				stringBuilder.AppendLine("<report build failed: " + ex.Message + ">");
			}
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("==== end of dump ====");
			return stringBuilder.ToString();
		}

		private static void AppendException(StringBuilder builder, Exception exception)
		{
			for (int depth = 0; exception != null && depth < 10; exception = exception.InnerException, depth++)
			{
				if (depth > 0)
				{
					builder.AppendLine("---- InnerException[" + depth + "] ----");
				}
				builder.AppendLine("Type:      " + exception.GetType().FullName);
				builder.AppendLine("Message:   " + exception.Message);
				builder.AppendLine("HResult:   0x" + exception.HResult.ToString("X8"));
				string stackTrace = exception.StackTrace;
				builder.AppendLine("StackTrace:" + Environment.NewLine + (stackTrace ?? "<none>"));
				builder.AppendLine();
			}
		}

		private static void PruneOldCrashFiles(string directory)
		{
			try
			{
				IEnumerable<string> files = Directory.GetFiles(directory, "crash-*.log").OrderByDescending((string x) => x, StringComparer.Ordinal);
				int num = 0;
				foreach (string item in files)
				{
					num++;
					if (num > MaxCrashFiles)
					{
						try
						{
							File.Delete(item);
						}
						catch
						{
						}
					}
				}
			}
			catch
			{
			}
		}
	}
}
