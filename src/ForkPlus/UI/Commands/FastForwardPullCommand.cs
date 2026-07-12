using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Commands
{
	public class FastForwardPullCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Fast-Forward Pull";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, LocalBranch localBranch)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			RepositoryData repositoryData = repositoryUserControl.RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			string upstreamFullReference = localBranch.UpstreamFullReference;
			if (upstreamFullReference == null)
			{
				return;
			}
			RemoteBranch remoteBranch = IReadOnlyListExtensions.FirstItem(repositoryData.References.RemoteBranches, (RemoteBranch x) => x.FullReference == upstreamFullReference);
			if (remoteBranch == null)
			{
				string text = localBranch.UpstreamFullReference.Replace("refs/remotes/", "");
				new ErrorWindow(PreferencesLocalization.FormatCurrent("Remote branch '{0}' doesn't exist. It might be removed, but '{1}' refers to it.", text, localBranch.Name)).ShowDialog();
				return;
			}
			SubmodulesToUpdate submodulesToUpdate = repositoryUserControl.SubmodulesToUpdate();
			repositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Pull '{0}' into '{1}'", remoteBranch.Name, localBranch.Name), delegate(JobMonitor monitor)
			{
				GitCommandResult requestResult = PerformPull(gitModule, remoteBranch, localBranch, submodulesToUpdate, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (!requestResult.Succeeded && !monitor.IsCanceled)
					{
						new ErrorWindow(repositoryUserControl, requestResult.Error).ShowDialog();
					}
					if (localBranch.IsActive)
					{
						repositoryUserControl.Invalidate(SubDomain.Head);
					}
					repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.References, new RevisionSelector.Sha(remoteBranch.Sha));
				});
			});
		}

		private GitCommandResult PerformPull(GitModule gitModule, RemoteBranch remoteBranch, LocalBranch localBranch, SubmodulesToUpdate submodulesToUpdate, JobMonitor monitor)
		{
			GitCommandResult gitCommandResult = new FastForwardPullGitCommand().Execute(gitModule, remoteBranch, localBranch, monitor);
			GitCommandResult gitCommandResult2 = GitCommandResult.Success();
			if (submodulesToUpdate.Length > 0)
			{
				monitor.Update(0.0, "Updating submodules...");
				gitCommandResult2 = new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
			}
			if (!gitCommandResult.Succeeded)
			{
				monitor.Fail(PreferencesLocalization.Current("Pull failed"));
				return gitCommandResult;
			}
			if (!gitCommandResult2.Succeeded)
			{
				monitor.Fail(PreferencesLocalization.Current("Update submodules failed"));
				return gitCommandResult2;
			}
			monitor.Success("Everything is up to date");
			return gitCommandResult;
		}
	}
}
