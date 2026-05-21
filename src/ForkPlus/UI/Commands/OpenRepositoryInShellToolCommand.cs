using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class OpenRepositoryInShellToolCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Open In " + ForkPlusSettings.Default.ShellTool.DisplayName;

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.T, ModifierKeys.Alt | ModifierKeys.Control);


		public KeyGesture SecondaryShortcut => null;

		public static CommandDescriptor[] PublicCommands => new CommandDescriptor[1]
		{
			new CommandDescriptor("Open In " + ForkPlusSettings.Default.ShellTool.DisplayName, new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					MainWindow.Commands.OpenRepositoryInShellTool.Execute(gitModule);
				}
			})
		};

		public void Execute(GitModule gitModule)
		{
			Execute(gitModule.Path);
		}

		public void Execute(string path)
		{
			ShellTool shellTool = ForkPlusSettings.Default.ShellTool;
			string applicationPath = shellTool.ApplicationPath;
			if (!File.Exists(applicationPath))
			{
				Log.Error("Cannot find shellToolPath at '" + applicationPath + "'");
				new ErrorWindow("Cannot find shellToolPath at '" + applicationPath + "'").ShowDialog();
				return;
			}
			Process process = new Process
			{
				StartInfo = new ProcessStartInfo(applicationPath)
				{
					WorkingDirectory = path,
					Arguments = shellTool.Arguments
				}
			};
			try
			{
				process.Start();
			}
			catch (Exception ex)
			{
				Log.Error("Cannot start '" + applicationPath + "'", ex);
				new ErrorWindow(ex.Message).ShowDialog();
			}
		}
	}
}
