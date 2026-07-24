using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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
			// 阶段 4.5：WPF System.Windows.Application.Current.Shutdown() → Avalonia IClassicDesktopStyleApplicationLifetime.Shutdown()。
			(Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
		}
	}
}
