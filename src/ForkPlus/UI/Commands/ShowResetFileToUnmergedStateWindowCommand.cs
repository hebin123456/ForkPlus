using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowResetFileToUnmergedStateWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Reset File to Unmerged State...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, [Null] ChangedFile selectedFile)
		{
			if (selectedFile != null && new MessageBoxWindow("Do you want reset file to unmerged state?", "This will reset conflict resolution and return the file to the unmerged state.", "Unmerge", "Cancel", showCancelButton: true, 500.0).ShowDialog().GetValueOrDefault())
			{
				GitCommandResult gitCommandResult = new ResetFileToUnmergedStateGitCommand().Execute(gitModule, selectedFile);
				if (!gitCommandResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, gitCommandResult.Error).ShowDialog();
				}
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status, null, RepositoryViewMode.CommitViewMode);
			}
		}
	}
}
