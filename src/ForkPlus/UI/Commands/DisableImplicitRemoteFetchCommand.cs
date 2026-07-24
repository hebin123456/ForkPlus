using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class DisableImplicitRemoteFetchCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, Remote remote, bool disable)
		{
			GitCommandResult gitCommandResult = new DisableImplicitRemoteFetchGitCommand().Execute(gitModule, remote, disable);
			if (!gitCommandResult.Succeeded)
			{
				new ErrorWindow(repositoryUserControl, gitCommandResult.Error).ShowDialog();
			}
			repositoryUserControl.InvalidateAndRefresh(SubDomain.Remotes);
		}
	}
}
