using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowCheckoutRevisionWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Checkout Commit...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, IGitPoint gitPoint, Sha sha)
		{
			if (repositoryUserControl.RepositoryStatus == null || repositoryUserControl.RepositoryData == null)
			{
				return;
			}
			CheckoutRevisionWindow checkoutRevisionWindow = new CheckoutRevisionWindow(repositoryUserControl, gitPoint, sha);
			if (checkoutRevisionWindow.ShowDialog().GetValueOrDefault())
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Head | SubDomain.Stashes | SubDomain.Submodules | SubDomain.Worktrees | SubDomain.BugtrackerSettings | SubDomain.CustomCommands | SubDomain.References, new RevisionSelector.Head());
				if (!checkoutRevisionWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, checkoutRevisionWindow.GitResult.Error).ShowDialog();
				}
			}
		}
	}
}
