using System;

namespace ForkPlus.UI
{
	public class StartupTimeReporter
	{
		private static DateTime _processStartTime;

		private static DateTime _appStartTime;

		private static DateTime _mainWindowCreatedTime;

		private static DateTime _mainWindowLoadedTime;

		public static void AppStarted(DateTime processStartTime)
		{
			_processStartTime = processStartTime;
			_appStartTime = DateTime.Now;
		}

		public static void MainWindowCreated()
		{
			_mainWindowCreatedTime = DateTime.Now;
		}

		public static void MainWindowLoaded()
		{
			_mainWindowLoadedTime = DateTime.Now;
		}

		public static void UIReady()
		{
			DateTime now = DateTime.Now;
			TimeSpan timeSpan = _appStartTime - _processStartTime;
			TimeSpan timeSpan2 = _mainWindowCreatedTime - _appStartTime;
			TimeSpan timeSpan3 = _mainWindowLoadedTime - _mainWindowCreatedTime;
			TimeSpan timeSpan4 = now - _mainWindowLoadedTime;
			TimeSpan timeSpan5 = now - _processStartTime;
			Log.Info($"App start: {timeSpan.TotalMilliseconds:F0}ms, AppInitialization: {timeSpan2.TotalMilliseconds:F0}ms, MainWindowOpening: {timeSpan3.TotalMilliseconds:F0}ms, MainWindowActivation: {timeSpan4.TotalMilliseconds:F0}ms, Total: {timeSpan5.TotalMilliseconds:F0}ms");
		}
	}
}
