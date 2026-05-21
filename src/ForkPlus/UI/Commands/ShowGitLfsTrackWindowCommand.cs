using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowGitLfsTrackWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Add Track Pattern...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, string initialPattern = "")
		{
			if (gitModule == null)
			{
				return;
			}
			GitLfsTrackWindow gitLfsTrackWindow = new GitLfsTrackWindow(gitModule, initialPattern);
			if (gitLfsTrackWindow.ShowDialog().GetValueOrDefault())
			{
				if (!gitLfsTrackWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, gitLfsTrackWindow.GitResult.Error).ShowDialog();
				}
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status);
			}
		}
	}
}
