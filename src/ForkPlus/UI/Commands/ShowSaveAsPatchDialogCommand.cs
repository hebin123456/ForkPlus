using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowSaveAsPatchDialogCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Save as Patch…";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, ChangedFile[] changedFiles, bool amend)
		{
			HashSet<ChangedFile> source = new HashSet<ChangedFile>(changedFiles.Where((ChangedFile x) => !x.IsDirectory));
			string initialDirectory = ForkPlusSettings.Default.RecentPatchDirectory ?? ForkPlus.RepositoryManager.Instance.DefaultSourceDir();
			string defaultFileName = gitModule.RepositoryName + "-" + DateTime.Now.ToString("HH-mm-ss");
			if (OpenDialog.SelectPatchSaveLocation(MainWindow.Instance, "Save patch as...", initialDirectory, defaultFileName, out var filePath))
			{
				GitCommandResult<string> gitCommandResult = new CreatePatchGitCommand().Execute(gitModule, source.ToArray(), amend);
				if (!gitCommandResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, gitCommandResult.Error).ShowDialog();
				}
				try
				{
					File.WriteAllText(filePath, gitCommandResult.Result);
					ForkPlusSettings.Default.RecentPatchDirectory = Path.GetDirectoryName(filePath);
				}
				catch (Exception ex)
				{
					Log.Error("Failed to write patch to '" + filePath + "'", ex);
				}
			}
		}
	}
}
