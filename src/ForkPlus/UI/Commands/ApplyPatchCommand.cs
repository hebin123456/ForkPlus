using System;
using System.IO;
using System.Windows.Input;
using ForkPlus.Services;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ApplyPatchCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Apply Patch…", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				RepositoryUserControl.Commands.ApplyPatch.Execute(repositoryUserControl);
			})
		};

		public string Title => "Apply Patch…";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut { get; }

		public void Execute(RepositoryUserControl repositoryUserControl)
		{
			string initialDirectory = ForkPlusSettings.Default.RecentPatchDirectory ?? ForkPlus.RepositoryManager.Instance.SourceDirs.FirstItem() ?? Environment.ExpandEnvironmentVariables("%userprofile%");
			if (ServiceLocator.FileSystemDialog.SelectFile("Select patch", initialDirectory, "Git Patch", "*.*", out var filePath))
			{
				ForkPlusSettings.Default.RecentPatchDirectory = Path.GetDirectoryName(filePath);
				new ShowApplyPatchWindowCommand().Execute(repositoryUserControl, filePath);
			}
		}

		public void Execute(RepositoryUserControl repositoryUserControl, string patchPath)
		{
			ForkPlusSettings.Default.RecentPatchDirectory = Path.GetDirectoryName(patchPath);
			new ShowApplyPatchWindowCommand().Execute(repositoryUserControl, patchPath);
		}
	}
}
