using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowResetBranchWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Reset Branch";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, [Null] LocalBranch activeBranch, Revision destination)
		{
			ResetBranchWindow resetBranchWindow = new ResetBranchWindow(repositoryUserControl, activeBranch, destination);
			if (resetBranchWindow.ShowDialog().GetValueOrDefault())
			{
				if (!resetBranchWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, resetBranchWindow.GitResult.Error).ShowDialog();
				}
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Revisions | SubDomain.Head | SubDomain.Submodules | SubDomain.BugtrackerSettings | SubDomain.CustomCommands | SubDomain.References, new RevisionSelector.Sha(destination.Sha));
			}
		}
	}
}
