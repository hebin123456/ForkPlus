using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowEditRemoteWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Edit Remote...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, Remote remote)
		{
			EditRemoteWindow editRemoteWindow = new EditRemoteWindow(repositoryUserControl, gitModule, remote);
			if (editRemoteWindow.ShowDialog().GetValueOrDefault())
			{
				if (!editRemoteWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, editRemoteWindow.GitResult.Error).ShowDialog();
				}
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Remotes | SubDomain.References);
			}
		}
	}
}
