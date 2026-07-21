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
		public string InstanceDirectory => App.InstanceDirectory;
		public int ProcessId => App.ProcessId;
		public string ProcessIdString => App.ProcessIdString;
		public string UserAgent => App.UserAgent;
		public string ForkCredentialHelperPath => App.ForkCredentialHelperPath;
		public string ForkDirectoryPath => App.ForkDirectoryPath;

		public void Shutdown()
		{
			System.Windows.Application.Current?.Dispatcher.Invoke(() =>
			{
				System.Windows.Application.Current.Shutdown();
			});
		}
	}
}
