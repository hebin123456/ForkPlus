using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class ShowRemoveRemoteWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => null;

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, Remote remote)
		{
			if (!new MessageBoxWindow("Are you sure you want to delete reference to remote '" + remote.Name + "'?", "Do you want to delete '" + remote.Name + "'?", "Delete", "Cancel", showCancelButton: true, 550.0).ShowDialog().GetValueOrDefault())
			{
				return;
			}
			repositoryUserControl.JobQueue.Add(ServiceLocator.Localization.FormatCurrent("Delete remote '{0}'", remote.Name), delegate(JobMonitor monitor)
			{
				GitCommandResult removeRemoteResult = new RemoveRemoteGitCommand().Execute(gitModule, remote, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (!removeRemoteResult.Succeeded && !monitor.IsCanceled)
					{
						new ErrorWindow(repositoryUserControl, removeRemoteResult.Error).ShowDialog();
					}
					repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.Remotes | SubDomain.References);
				});
			}, JobFlags.SaveToLog);
		}
	}
}
