using System;
using ForkPlus.Services;
using Avalonia.Threading;

namespace ForkPlus.Services.Wpf
{
	public class WpfAppContext : IAppContext
	{
		public string AppDataDirectory => App.ForkDirectoryPath;
		public string ForkDataDirectoryPath => App.ForkDataDirectoryPath;
		public string RepositoriesFilePath => App.RepositoriesFilePath;
		public Version OSVersion => App.OSVersion;

		public void Shutdown()
		{
			global::Avalonia.Application.Current?.Dispatcher.Invoke(() =>
			{
				global::ForkPlus.UI.WpfCompat.WpfApp.Shutdown();
			});
		}
	}
}
