using System.IO;
using NLog;
using NLog.Config;
using NLog.LayoutRenderers;
using NLog.Targets;

namespace ForkPlus
{
	public class DebugLoggingConfiguration : LoggingConfiguration
	{
		public DebugLoggingConfiguration()
		{
			// NLog v5.2 起 LayoutRenderer.Register<T>(string) 已过时，改用
			// LogManager.Setup().SetupExtensions() 注册自定义 LayoutRenderer。
			LogManager.Setup().SetupExtensions(s => s.RegisterLayoutRenderer<LevelIconLayoutRenderer>("levelIcon"));
			DebuggerTarget debuggerTarget = new DebuggerTarget("Visual Studio Debugger");
			AddTarget("debugger", debuggerTarget);
			debuggerTarget.Layout = "${levelIcon} ${message}";
			FileTarget fileTarget = new FileTarget("AppData log file");
			AddTarget("file", fileTarget);
			fileTarget.Layout = "${levelIcon} ${date:format=yyyy-MM-dd HH\\:mm\\:ss.fff} ${message}";
			fileTarget.FileName = Path.Combine(App.ForkDirectoryPath, "logs", "fork.log");
			fileTarget.DeleteOldFileOnStartup = true;
			LoggingRule item = new LoggingRule("*", LogLevel.Debug, debuggerTarget);
			base.LoggingRules.Add(item);
			LoggingRule item2 = new LoggingRule("*", LogLevel.Debug, fileTarget);
			base.LoggingRules.Add(item2);
		}
	}
}
