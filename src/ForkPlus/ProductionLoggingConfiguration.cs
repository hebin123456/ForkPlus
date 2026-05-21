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
			LayoutRenderer.Register<LevelIconLayoutRenderer>("levelIcon");
			FileTarget fileTarget = new FileTarget("AppData log file");
			AddTarget("file", fileTarget);
			fileTarget.Layout = "${levelIcon} ${date:format=yyyy-MM-dd HH\\:mm\\:ss.fff} ${message}";
			fileTarget.FileName = Path.Combine(App.ForkDirectoryPath, "logs", "fork.log");
			fileTarget.DeleteOldFileOnStartup = true;
			LoggingRule item = new LoggingRule("*", LogLevel.Debug, fileTarget);
			base.LoggingRules.Add(item);
		}
	}
}
