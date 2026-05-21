using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ResolveConflictWithExistingVersionCommand : IUICommand, IForkPlusCommand
	{
		public string Title => null;

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, ChangedFile[] changedFiles, UnmergedFileVersionType version)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			GitCommandResult gitCommandResult = GitCommandResult.Success();
			foreach (ChangedFile file in changedFiles)
			{
				gitCommandResult = new ResolveConflictGitCommand().Execute(gitModule, file, version);
				if (!gitCommandResult.Succeeded)
				{
					break;
				}
			}
			if (!gitCommandResult.Succeeded)
			{
				new ErrorWindow(repositoryUserControl, gitCommandResult.Error).ShowDialog();
			}
			repositoryUserControl.InvalidateAndRefresh(SubDomain.Status, null, RepositoryViewMode.CommitViewMode);
		}
	}
}
