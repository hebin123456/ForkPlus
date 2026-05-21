using System;
using NLog;

namespace ForkPlus
{
	public static class Log
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
			Logger.Error(FormatExceptionMessage(message, ex));
		}

		public static void Warn(string message, Exception ex)
		{
			Logger.Warn(FormatExceptionMessage(message, ex));
		}

		private static string FormatExceptionMessage(string message, Exception ex)
		{
			if (ex == null)
			{
				return message;
			}
			return message + Environment.NewLine + ex;
		}
	}
}
