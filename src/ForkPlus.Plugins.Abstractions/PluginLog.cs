using System;
using NLog;

namespace ForkPlus.Plugins
{
	/// <summary>
	/// 插件工程自带的日志门面：与主工程 ForkPlus.Log 同款 NLog 封装。
	/// NLog 配置由宿主进程初始化（同一个 NLog.config），插件运行在宿主内时日志自动汇入同一输出。
	/// v5.0.0 拆分后 public：契约（Abstractions）→ 共享组件（Ui）→ 各插件跨工程使用。
	/// </summary>
	public static class PluginLog
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static void Debug(string message)
		{
			Logger.Debug(message);
		}

		public static void Error(string message)
		{
			Logger.Error(message);
		}

		public static void Info(string message)
		{
			Logger.Info(message);
		}

		public static void Warn(string message)
		{
			Logger.Warn(message);
		}

		public static void Error(string message, Exception ex)
		{
			Logger.Error(message + Environment.NewLine + ex);
		}

		public static void Warn(string message, Exception ex)
		{
			Logger.Warn(message + Environment.NewLine + ex);
		}
	}
}
