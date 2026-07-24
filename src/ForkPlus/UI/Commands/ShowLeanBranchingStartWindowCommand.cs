using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowLeanBranchingStartWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Start Branch...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, Branch mainBranch)
		{
			if (repositoryUserControl.GitModule == null || repositoryUserControl.RepositoryStatus == null || repositoryUserControl.RepositoryData == null)
			{
				return;
			}
			LeanBranchingStartWindow leanBranchingStartWindow = new LeanBranchingStartWindow(repositoryUserControl, mainBranch);
			if (leanBranchingStartWindow.ShowDialog().GetValueOrDefault())
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Head | SubDomain.Stashes | SubDomain.Submodules | SubDomain.BugtrackerSettings | SubDomain.CustomCommands | SubDomain.References, new RevisionSelector.Sha(mainBranch.Sha));
				if (!leanBranchingStartWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, leanBranchingStartWindow.GitResult.Error).ShowDialog();
				}
			}
		}
	}
}
