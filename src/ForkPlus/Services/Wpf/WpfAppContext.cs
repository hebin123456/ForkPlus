using System;
using ForkPlus.Services;

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
			System.Windows.Application.Current?.Dispatcher.Invoke(() =>
			{
				System.Windows.Application.Current.Shutdown();
			});
		}
	}
}
