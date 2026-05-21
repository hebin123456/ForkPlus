using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
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

		public string Title => "Open In File Explorer";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.O, ModifierKeys.Alt | ModifierKeys.Control);


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
				Process process = new Process();
				ProcessStartInfo startInfo = new ProcessStartInfo(repositoryPath);
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
