using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowCheckoutBranchAsWorktreeWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Checkout Branch as Worktree...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, LocalBranch branch)
		{
			if (repositoryUserControl.RepositoryData == null || repositoryUserControl.GitModule == null)
			{
				return;
			}
			CheckoutBranchAsWorktreeWindow checkoutBranchAsWorktreeWindow = new CheckoutBranchAsWorktreeWindow(repositoryUserControl, branch);
			if (checkoutBranchAsWorktreeWindow.ShowDialog().GetValueOrDefault())
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Worktrees);
				if (!checkoutBranchAsWorktreeWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, checkoutBranchAsWorktreeWindow.GitResult.Error).ShowDialog();
				}
			}
		}
	}
}
