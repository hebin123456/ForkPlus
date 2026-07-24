using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowAddRemoteWindowCommand : IUICommand, IForkPlusCommand
	{
		public KeyGesture Shortcut => null;

		public string Title => "Add New Remote...";

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			EditRemoteWindow editRemoteWindow = new EditRemoteWindow(repositoryUserControl, gitModule);
			if (editRemoteWindow.ShowDialog().GetValueOrDefault())
			{
				if (!editRemoteWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, editRemoteWindow.GitResult.Error).ShowDialog();
				}
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Remotes);
			}
		}
	}
}
