using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class QuickFetchCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Quick Fetch";

		public KeyGesture Shortcut => new KeyGesture(Key.F, ModifierKeys.Alt | ModifierKeys.Control | ModifierKeys.Shift);

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			RepositoryData repositoryData = repositoryUserControl.RepositoryData;
			if (repositoryData != null)
			{
				Remote remote = IReadOnlyListExtensions.FirstItem(repositoryData.Remotes.Items, (Remote x) => x.Name == Consts.Git.DefaultRemoteName) ?? repositoryData.Remotes.Items.FirstItem();
				if (remote != null)
				{
					QuickFetch(repositoryUserControl, gitModule, remote);
				}
				else
				{
					new FetchWindow(repositoryUserControl, gitModule, remote).ShowDialog();
				}
			}
		}

		private void QuickFetch(RepositoryUserControl repositoryUserControl, GitModule gitModule, Remote remote)
		{
			bool fetchAllRemotes = ForkPlusSettings.Default.Fetch_FetchAllRemotes;
			bool fetchAllTags = ForkPlusSettings.Default.FetchAllTags;
			string name = (fetchAllRemotes ? "Fetch all" : ("Fetch '" + remote.Name + "'"));
			repositoryUserControl.JobQueue.Add(name, delegate(JobMonitor monitor)
			{
				GitCommandResult fetchResult = new FetchGitCommand().Execute(gitModule, remote, fetchAllRemotes, monitor, noPrompt: false, fetchAllTags);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (!fetchResult.Succeeded && !monitor.IsCanceled)
					{
						new ErrorWindow(repositoryUserControl, fetchResult.Error).ShowDialog();
					}
					repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.References);
				});
			});
		}
	}
}
