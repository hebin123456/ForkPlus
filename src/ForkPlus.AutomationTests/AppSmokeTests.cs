using System;
using System.IO;
using FlaUI.Core;
using FlaUI.UIA3;
using Xunit;

namespace ForkPlus.AutomationTests
{
	public class AppSmokeTests
	{
		[Fact]
		public void MainWindow_StartsAndExposesWindow()
		{
			string exePath = ResolveAppPath();
			if (!File.Exists(exePath))
			{
				return;
			}

			using (Application app = Application.Launch(exePath))
			using (var automation = new UIA3Automation())
			{
				var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(15));
				Assert.NotNull(window);
				Assert.Contains("ForkPlus", window.Title ?? "");
				window.Close();
			}
		}

		private static string ResolveAppPath()
		{
			string configuredPath = Environment.GetEnvironmentVariable("FORKPLUS_AUTOMATION_EXE");
			if (!string.IsNullOrWhiteSpace(configuredPath))
			{
				return configuredPath;
			}
			return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\ForkPlus\bin\Debug\net472\ForkPlus.exe"));
		}
	}
}
