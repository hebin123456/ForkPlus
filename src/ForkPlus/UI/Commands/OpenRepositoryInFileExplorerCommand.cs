using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class OpenRepositoryInFileExplorerCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Open In File Explorer", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					MainWindow.Commands.OpenRepositoryInFileExplorer.Execute(gitModule);
				}
			})
		};

		// Migration note：平台化文案（macOS 期望 Finder 措辞）。
		public string Title => OperatingSystem.IsMacOS() ? "Open in Finder" : (OperatingSystem.IsWindows() ? "Open In File Explorer" : "Open in File Manager");

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.O, global::Avalonia.Input.KeyModifiers.Alt | global::Avalonia.Input.KeyModifiers.Control);


		public KeyGesture SecondaryShortcut => null;

		public void Execute([Null] string repositoryPath)
		{
			if (Keyboard.IsKeyDown(Key.RightAlt) || string.IsNullOrEmpty(repositoryPath) || !Directory.Exists(repositoryPath))
			{
				return;
			}
			Log.Info("Open in file explorer '" + repositoryPath + "'");
			try
			{
				// .NET 10 起 UseShellExecute 默认从 true 改为 false，传文件夹路径作 FileName
				// 会因"非可执行文件"抛 Win32Exception，必须显式置 true 才能走 Shell 打开文件夹。
				Process process = new Process();
				ProcessStartInfo startInfo = new ProcessStartInfo(repositoryPath) { UseShellExecute = true };
				process.StartInfo = startInfo;
				process.Start();
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to open '" + repositoryPath + "' in file explorer", ex);
			}
		}

		public void Execute(GitModule gitModule)
		{
			Execute(gitModule.Path);
		}
	}
}
