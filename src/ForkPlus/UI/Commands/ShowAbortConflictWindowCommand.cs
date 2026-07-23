using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Commands.LeanBranching;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class ShowAbortConflictWindowCommand : IForkPlusCommand
	{
		public void Execute(RepositoryUserControl repositoryUserControl)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			RepositoryStatus repositoryStatus = repositoryUserControl.RepositoryStatus;
			if (repositoryStatus == null)
			{
				return;
			}
			SubmodulesToUpdate submodulesToUpdate = repositoryUserControl.SubmodulesToUpdate();
			RepositoryState repositoryState = repositoryStatus.RepositoryState;
			if (repositoryState is RepositoryState.MergeInProgress)
			{
				if (new MessageBoxWindow("Do you want to abort the merge?", "All uncommitted changes will be lost.", "Abort merge", "Cancel", showCancelButton: true, 600.0, showWarningIcon: true).ShowDialog().GetValueOrDefault())
				{
					GitCommandResult gitCommandResult = new AbortMergeGitCommand().Execute(gitModule);
					if (!gitCommandResult.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, gitCommandResult.Error).ShowDialog();
					}
					gitModule.Settings.DraftMessage = "";
					gitModule.Settings.Save();
					UpdateSubmodulesAndRefresh(repositoryUserControl, submodulesToUpdate, SubDomain.Status | SubDomain.Revisions | SubDomain.References);
				}
			}
			else if (repositoryState is RepositoryState.RebaseInProgress)
			{
				if (new MessageBoxWindow("Do you want to abort the rebase?", "Undo the rebase and check out the original branch?", "Abort rebase", "Cancel", showCancelButton: true, 600.0, showWarningIcon: true).ShowDialog().GetValueOrDefault())
				{
					GitCommandResult gitCommandResult2 = new AbortRebaseGitCommand().Execute(gitModule);
					if (!gitCommandResult2.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, gitCommandResult2.Error).ShowDialog();
					}
					if (LeanBranching.IsSyncInProgress(gitModule))
					{
						LeanBranching.AbortSync(gitModule);
					}
					gitModule.Settings.DraftMessage = "";
					gitModule.Settings.Save();
					repositoryUserControl.UncheckAmendCheckBox();
					UpdateSubmodulesAndRefresh(repositoryUserControl, submodulesToUpdate, SubDomain.Status | SubDomain.Revisions | SubDomain.Head | SubDomain.References);
				}
			}
			else if (repositoryState is RepositoryState.CherryPickInProgress)
			{
				if (new MessageBoxWindow("Do you want to abort cherry-pick?", "All uncommitted changes will be lost.", "Abort cherry-pick", "Cancel", showCancelButton: true, 600.0, showWarningIcon: true).ShowDialog().GetValueOrDefault())
				{
					GitCommandResult gitCommandResult3 = new AbortMergeGitCommand().Execute(gitModule);
					if (!gitCommandResult3.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, gitCommandResult3.Error).ShowDialog();
					}
					gitModule.Settings.DraftMessage = "";
					gitModule.Settings.Save();
					UpdateSubmodulesAndRefresh(repositoryUserControl, submodulesToUpdate, SubDomain.Status | SubDomain.Revisions | SubDomain.References);
				}
			}
			else if (repositoryState is RepositoryState.RevertInProgress)
			{
				if (new MessageBoxWindow("Do you want to abort revert?", "All uncommitted changes will be lost.", "Abort revert", "Cancel", showCancelButton: true, 600.0, showWarningIcon: true).ShowDialog().GetValueOrDefault())
				{
					GitCommandResult gitCommandResult4 = new AbortRevertGitCommand().Execute(gitModule);
					if (!gitCommandResult4.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, gitCommandResult4.Error).ShowDialog();
					}
					gitModule.Settings.DraftMessage = "";
					gitModule.Settings.Save();
					UpdateSubmodulesAndRefresh(repositoryUserControl, submodulesToUpdate, SubDomain.Status | SubDomain.Revisions | SubDomain.References);
				}
			}
			else if (repositoryState is RepositoryState.SequencerInProgress)
			{
				if (new MessageBoxWindow("Do you want to abort cherry-pick?", "All uncommitted changes will be lost.", "Abort cherry-pick", "Cancel", showCancelButton: true, 600.0, showWarningIcon: true).ShowDialog().GetValueOrDefault())
				{
					GitCommandResult gitCommandResult5 = new AbortCherryPickGitCommand().Execute(gitModule);
					if (!gitCommandResult5.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, gitCommandResult5.Error).ShowDialog();
					}
					gitModule.Settings.DraftMessage = "";
					gitModule.Settings.Save();
					UpdateSubmodulesAndRefresh(repositoryUserControl, submodulesToUpdate, SubDomain.Status | SubDomain.Revisions | SubDomain.References);
				}
			}
			else if (repositoryState is RepositoryState.AmInProgress)
			{
				if (new MessageBoxWindow("Do you want to abort applying patches?", "All uncommitted changes will be lost.", "Abort AM", "Cancel", showCancelButton: true, 600.0, showWarningIcon: true).ShowDialog().GetValueOrDefault())
				{
					GitCommandResult gitCommandResult6 = new AbortAmGitCommand().Execute(gitModule);
					if (!gitCommandResult6.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, gitCommandResult6.Error).ShowDialog();
					}
					gitModule.Settings.DraftMessage = "";
					gitModule.Settings.Save();
					UpdateSubmodulesAndRefresh(repositoryUserControl, submodulesToUpdate, SubDomain.All);
				}
			}
			else if (repositoryState is RepositoryState.UnmergedIndex)
			{
				if (new MessageBoxWindow("Do you want to abort the merge?", "All uncommitted changes will be lost.", "Abort", "Cancel", showCancelButton: true, 600.0, showWarningIcon: true).ShowDialog().GetValueOrDefault())
				{
					GitCommandResult gitCommandResult7 = new AbortMergeGitCommand().Execute(gitModule);
					if (!gitCommandResult7.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, gitCommandResult7.Error).ShowDialog();
					}
					gitModule.Settings.DraftMessage = "";
					gitModule.Settings.Save();
					UpdateSubmodulesAndRefresh(repositoryUserControl, submodulesToUpdate, SubDomain.Status | SubDomain.Revisions | SubDomain.References);
				}
			}
			else if (repositoryState is RepositoryState.SquashInProgress)
			{
				if (new MessageBoxWindow("Do you want to abort the merge?", "All uncommitted changes will be lost.", "Abort merge", "Cancel", showCancelButton: true, 600.0, showWarningIcon: true).ShowDialog().GetValueOrDefault())
				{
					GitCommandResult gitCommandResult8 = new AbortMergeGitCommand().Execute(gitModule);
					if (!gitCommandResult8.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, gitCommandResult8.Error).ShowDialog();
					}
					gitModule.Settings.DraftMessage = "";
					gitModule.Settings.Save();
					UpdateSubmodulesAndRefresh(repositoryUserControl, submodulesToUpdate, SubDomain.Status);
				}
			}
			else if (repositoryState is RepositoryState.BisectInProgress)
			{
				RepositoryUserControl.Commands.Bisect.Execute(repositoryUserControl, BisectGitCommand.BisectCommand.Reset);
			}
			else
			{
				Log.Error($"Abort for repository state '{repositoryState.GetType()}' cannot be handled.");
			}
		}

		private void UpdateSubmodulesAndRefresh(RepositoryUserControl repositoryUserControl, SubmodulesToUpdate submodulesToUpdate, SubDomain subdomainToRefresh)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			if (submodulesToUpdate.Length == 0)
			{
				repositoryUserControl.InvalidateAndRefresh(subdomainToRefresh);
				return;
			}
			repositoryUserControl.JobQueue.Add(ServiceLocator.Localization.Current("Updating submodules..."), delegate(JobMonitor monitor)
			{
				GitCommandResult updateSubmodulesResult = new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					repositoryUserControl.InvalidateAndRefresh(subdomainToRefresh);
					if (!updateSubmodulesResult.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, updateSubmodulesResult.Error).ShowDialog();
					}
				});
			});
		}
	}
}
