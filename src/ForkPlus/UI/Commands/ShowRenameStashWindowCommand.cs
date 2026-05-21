using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowRenameStashWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title { get; } = "Rename Stash...";


		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, StashRevision stash)
		{
			RenameStashWindow renameStashWindow = new RenameStashWindow(repositoryUserControl, stash);
			if (renameStashWindow.ShowDialog().GetValueOrDefault())
			{
				if (!renameStashWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, renameStashWindow.GitResult.Error).ShowDialog();
				}
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.Stashes, renameStashWindow.OutResultSha.HasValue ? new RevisionSelector.Sha(renameStashWindow.OutResultSha.Value) : null);
			}
		}
	}
}
