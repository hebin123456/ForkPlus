using System.Text;
using NLog;
using NLog.LayoutRenderers;

namespace ForkPlus
{
	[LayoutRenderer("levelIcon")]
	public class LevelIconLayoutRenderer : LayoutRenderer
	{
		protected override void Append(StringBuilder builder, LogEventInfo logEvent)
		{
			LogLevel level = logEvent.Level;
			if (level == LogLevel.Info)
			{
				builder.Append("\ud83d\udd37");
			}
			else if (level == LogLevel.Debug)
			{
				builder.Append("◽\ufe0f");
			}
			else if (level == LogLevel.Error)
			{
				builder.Append("❌");
			}
			else if (level == LogLevel.Warn)
			{
				builder.Append("⚠\ufe0f");
			}
			else if (level == LogLevel.Trace)
			{
				builder.Append(".");
			}
			else
			{
				builder.Append(level.ToString());
			}
		}
	}
}
