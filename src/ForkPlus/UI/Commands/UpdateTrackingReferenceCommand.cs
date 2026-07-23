using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class UpdateTrackingReferenceCommand : IUICommand, IForkPlusCommand
	{
		public string Title => null;

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, LocalBranch localBranch, RemoteBranch trackingReference)
		{
			// v3.4.1：状态栏标题国际化（之前是硬编码英文）
		string name = ((trackingReference == null) ? ServiceLocator.Localization.Current("Remove tracking reference") : ServiceLocator.Localization.Current("Update tracking reference"));
			repositoryUserControl.JobQueue.Add(name, delegate(JobMonitor monitor)
			{
				GitCommandResult updateTrackingReferenceResult = new UpdateTrackingReferenceGitCommand().Execute(gitModule, localBranch, trackingReference, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (!updateTrackingReferenceResult.Succeeded && !monitor.IsCanceled)
					{
						new ErrorWindow(repositoryUserControl, updateTrackingReferenceResult.Error).ShowDialog();
					}
					repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.References, new RevisionSelector.Sha(localBranch.Sha));
				});
			}, JobFlags.SaveToLog);
		}
	}
}
