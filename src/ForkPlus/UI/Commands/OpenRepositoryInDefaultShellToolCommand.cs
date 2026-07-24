using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.Commands
{
	public class OpenRepositoryInDefaultShellToolCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Open Git Bash";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute([Null] GitModule gitModule)
		{
			if (gitModule != null)
			{
				Execute(gitModule.Path);
			}
		}

		public void Execute(string path)
		{
			string applicationPath = new ShellTool.Default().ApplicationPath;
			if (!File.Exists(applicationPath))
			{
				Log.Error("Cannot find shellToolPath at '" + applicationPath + "'");
				return;
			}
			Process process = new Process
			{
				StartInfo = new ProcessStartInfo(applicationPath)
				{
					WorkingDirectory = path
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
