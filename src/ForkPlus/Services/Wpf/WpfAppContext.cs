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

		public string GitPath => App.GitPath;
		public string ShellPath => App.ShellPath;
		public string BashPath => App.BashPath;
		public string GitMmPath => App.GitMmPath;
		public int ProcessId => App.ProcessId;
		public string Version => App.Version;
		public string UserAgent => App.UserAgent;

		public void Shutdown()
		{
			System.Windows.Application.Current?.Dispatcher.Invoke(() =>
			{
				System.Windows.Application.Current.Shutdown();
			});
		}
	}
}
