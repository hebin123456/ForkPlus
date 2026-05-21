using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowDeleteWorktreeWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Delete...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, Worktree worktree)
		{
			DeleteWorktreeWindow deleteWorktreeWindow = new DeleteWorktreeWindow(repositoryUserControl, worktree);
			if (deleteWorktreeWindow.ShowDialog().GetValueOrDefault())
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Worktrees);
				if (!deleteWorktreeWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, deleteWorktreeWindow.GitResult.Error).ShowDialog();
				}
			}
		}
	}
}
