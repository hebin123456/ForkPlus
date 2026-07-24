using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowApplyStashWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => null;

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, StashRevision stash)
		{
			ApplyStashWindow applyStashWindow = new ApplyStashWindow(repositoryUserControl, stash);
			if (applyStashWindow.ShowDialog().GetValueOrDefault())
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Revisions | SubDomain.Head | SubDomain.Stashes, new RevisionSelector.Head());
				if (!applyStashWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, applyStashWindow.GitResult.Error).ShowDialog();
				}
			}
		}
	}
}
