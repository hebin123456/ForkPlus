using System.IO;
using NLog;
using NLog.Config;
using NLog.LayoutRenderers;
using NLog.Targets;

namespace ForkPlus
{
	public class ProductionLoggingConfiguration : LoggingConfiguration
	{
		public ProductionLoggingConfiguration()
		{
			// NLog v5.2 起 LayoutRenderer.Register<T>(string) 已过时，改用
			// LogManager.Setup().SetupExtensions() 注册自定义 LayoutRenderer。
			LogManager.Setup().SetupExtensions(s => s.RegisterLayoutRenderer<LevelIconLayoutRenderer>("levelIcon"));
			FileTarget fileTarget = new FileTarget("AppData log file");
			AddTarget("file", fileTarget);
			fileTarget.Layout = "${levelIcon} ${date:format=yyyy-MM-dd HH\\:mm\\:ss.fff} ${message}";
			fileTarget.FileName = Path.Combine(App.ForkDirectoryPath, "logs", "fork.log");
			// v4.0.5：原 DeleteOldFileOnStartup=true 会在每次启动时删掉上一会话的日志——
			// 崩溃后用户一重启应用，崩溃现场就没了，"把崩溃前的日志反馈给开发者"无从谈起。
			// 改为按天滚动归档 + 保留 14 份，既有界增长又不丢崩溃前的上下文
			// （崩溃瞬间的完整现场另见 CrashDumper 的 crash-*.log 独立落盘）。
			fileTarget.ArchiveEvery = FileArchivePeriod.Day;
			fileTarget.ArchiveFileName = Path.Combine(App.ForkDirectoryPath, "logs", "archive", "fork-${shortdate}.log");
			fileTarget.MaxArchiveFiles = 14;
			// 崩溃前缓冲落地：KeepFileOpen + AutoFlush 使每次写日志都即时刷到 OS
			// （性能足够：单文件 GUI 应用，无高并发写场景）。
			fileTarget.KeepFileOpen = true;
			fileTarget.AutoFlush = true;
			LoggingRule item = new LoggingRule("*", LogLevel.Debug, fileTarget);
			base.LoggingRules.Add(item);
		}
	}
}
