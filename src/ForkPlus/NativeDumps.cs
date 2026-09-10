using System;
using System.IO;
using System.Linq;

namespace ForkPlus
{
	/// <summary>
	/// v4.0.6 native 崩溃转储：启用 .NET 运行时自带的 createdump，让 SIGSEGV / SIGABRT 等
	/// native 层硬崩也留下可分析的转储文件（dump-*.dmp）。
	///
	/// 背景：v4.0.5 的 CrashDumper 只能捕获托管异常（crash-*.log）。native 层硬崩
	/// （Marshal.Copy 越界、Rust panic 穿栈等）发生时 CLR 的异常机制完全管不到，
	/// 进程直接被信号杀死——事后只有操作系统层面的记录，进程内零现场。
	///
	/// 方案：进程启动最早期（Program.Main 第一行）设置运行时诊断环境变量：
	///   DOTNET_DbgEnableMiniDump=1            native 崩溃时由运行时拉起 createdump；
	///   DOTNET_DbgMiniDumpType=2              WithHeap 转储（dotnet-dump/SOS 可直接
	///                                         clrstack 看 UI 线程托管栈）；
	///   DOTNET_DbgMiniDumpName=&lt;logs&gt;/dump-%p.dmp   落到应用日志目录（%p=PID），
	///                                         与 crash-*.log / freeze-*.log 同目录，
	///                                         一并打包反馈即完整现场。
	/// 用户已显式设置的同名变量不覆盖（尊重外部诊断配置）。启动时顺带清理过期转储
	/// （保留最近 5 份，WithHeap 转储可达数百 MB，防止磁盘被吃满）。
	/// 本类自身绝不抛异常（诊断路径不能成为新崩溃源）。
	/// </summary>
	public static class NativeDumps
	{
		public const string EnableEnvVariable = "DOTNET_DbgEnableMiniDump";
		public const string DumpTypeEnvVariable = "DOTNET_DbgMiniDumpType";
		public const string DumpNameEnvVariable = "DOTNET_DbgMiniDumpName";

		/// <summary>WithHeap（2）：含托管堆，dotnet-dump analyze 可还原线程栈与对象状态。</summary>
		private const string DumpTypeWithHeap = "2";

		private const int MaxDumpFiles = 5;

		// internal（InternalsVisibleTo）：回归测试把目录指向临时目录做保留策略验证。
		// 仅测试使用，生产恒为 null。
		private static string _dumpDirectoryOverrideForTests;

		/// <summary>native 转储目录（与 crash-*.log 同目录）。</summary>
		public static string DumpDirectory => _dumpDirectoryOverrideForTests ?? Path.Combine(App.ForkDirectoryPath, "logs");

		/// <summary>测试专用：临时重定向转储目录。传 null 恢复。finally 中务必复位。</summary>
		internal static string DumpDirectoryOverrideForTests
		{
			get => _dumpDirectoryOverrideForTests;
			set => _dumpDirectoryOverrideForTests = value;
		}

		/// <summary>
		/// 必须在 Program.Main 的第一行调用（早于任何可能崩溃的初始化）。
		/// 幂等，重复调用安全。
		/// </summary>
		public static void Initialize()
		{
			try
			{
				string directory = DumpDirectory;
				Directory.CreateDirectory(directory);
				EnsureEnvVariable(EnableEnvVariable, "1");
				EnsureEnvVariable(DumpTypeEnvVariable, DumpTypeWithHeap);
				// %p 由 createdump 替换为 PID；跨进程多次崩溃各写各的文件。
				EnsureEnvVariable(DumpNameEnvVariable, Path.Combine(directory, "dump-%p.dmp"));
				PruneOldDumps();
			}
			catch
			{
				// 诊断初始化失败（磁盘满/权限）静默——不能影响应用启动。
			}
		}

		/// <summary>清理过期 native 转储（保留最近 MaxDumpFiles 份）。幂等，自身异常全部吞掉。</summary>
		public static void PruneOldDumps()
		{
			try
			{
				string directory = DumpDirectory;
				if (!Directory.Exists(directory))
				{
					return;
				}
				string[] files = Directory.GetFiles(directory, "dump-*.dmp");
				foreach (string file in files.OrderByDescending((string x) => x, StringComparer.Ordinal).Skip(MaxDumpFiles))
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

		/// <summary>环境变量仅在未设置时写入默认值（外部诊断配置优先）。</summary>
		internal static void EnsureEnvVariable(string name, string value)
		{
			if (Environment.GetEnvironmentVariable(name) == null)
			{
				Environment.SetEnvironmentVariable(name, value);
			}
		}
	}
}
