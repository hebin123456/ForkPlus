using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Threading;

namespace ForkPlus.UI.Commands
{
	public class ShowRemoveRemoteWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => null;

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, Remote remote)
		{
			RemoveRemoteWindow removeRemoteWindow = new RemoveRemoteWindow(repositoryUserControl, remote);
			if (!removeRemoteWindow.ShowDialog().GetValueOrDefault())
			{
				return;
			}
			if (!removeRemoteWindow.GitResult.Succeeded)
			{
				new ErrorWindow(repositoryUserControl, removeRemoteWindow.GitResult.Error).ShowDialog();
			}
			repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.Remotes | SubDomain.References);
		}
	}
}
