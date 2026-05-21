using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowChangeTrackingReferenceWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Change Tracking Reference...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, [Null] LocalBranch localBranch, [Null] RepositoryData repositoryData)
		{
			if (localBranch == null || repositoryData == null)
			{
				return;
			}
			ChangeRemoteTrackingWindow changeRemoteTrackingWindow = new ChangeRemoteTrackingWindow(gitModule, localBranch, repositoryData.References);
			if (changeRemoteTrackingWindow.ShowDialog().GetValueOrDefault())
			{
				if (!changeRemoteTrackingWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, changeRemoteTrackingWindow.GitResult.Error).ShowDialog();
				}
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.References, new RevisionSelector.Sha(localBranch.Sha));
			}
		}
	}
}
