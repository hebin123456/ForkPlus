using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class QuickPushCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Quick Push";

		public KeyGesture Shortcut => new KeyGesture(Key.P, ModifierKeys.Alt | ModifierKeys.Control | ModifierKeys.Shift);

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl)
		{
			RepositoryData repositoryData = repositoryUserControl?.RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			LocalBranch activeBranch = repositoryData.References.ActiveBranch;
			if (activeBranch == null)
			{
				return;
			}
			RemoteBranch[] remoteBranches = repositoryData.References.RemoteBranches;
			Remote[] items = repositoryData.Remotes.Items;
			string upstream = activeBranch.UpstreamFullReference;
			if (upstream != null)
			{
				RemoteBranch activeUpstream = IReadOnlyListExtensions.FirstItem(remoteBranches, (RemoteBranch x) => x.FullReference == upstream);
				if (activeUpstream != null)
				{
					Remote remote = IReadOnlyListExtensions.FirstItem(items, (Remote x) => x.Name == activeUpstream.Remote);
					if (remote != null)
					{
						QuickPush(repositoryUserControl, activeBranch, remote, track: false);
						return;
					}
				}
			}
			if (items.Length == 1)
			{
				Remote remote2 = items.FirstItem();
				if (remote2 != null)
				{
					QuickPush(repositoryUserControl, activeBranch, remote2, track: true);
					return;
				}
			}
			new PushWindow(repositoryUserControl, null, activeBranch).ShowDialog();
		}

		private void QuickPush(RepositoryUserControl repositoryUserControl, LocalBranch localBranch, Remote remote, bool track)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			repositoryUserControl.JobQueue.Add(ServiceLocator.Localization.FormatCurrent("Push '{0}' to '{1}'", localBranch.Name, remote.Name), delegate(JobMonitor monitor)
			{
				bool push_PushAllTags = ForkPlusSettings.Default.Push_PushAllTags;
				bool force = false;
				GitCommandResult pushResult = new PushGitCommand().Execute(gitModule, remote.Name, localBranch, null, null, push_PushAllTags, force, track, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (!pushResult.Succeeded && !monitor.IsCanceled)
					{
						new ErrorWindow(repositoryUserControl, pushResult.Error).ShowDialog();
					}
					repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.References);
				});
			});
		}
	}
}
