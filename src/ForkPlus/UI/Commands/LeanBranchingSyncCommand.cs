using Avalonia.Input;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Commands.LeanBranching;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class LeanBranchingSyncCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Sync";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl)
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
			RepositoryStatus repositoryStatus = repositoryUserControl.RepositoryStatus;
			if (repositoryStatus == null)
			{
				return;
			}
			CommitGraphCache commitGraphCache = repositoryUserControl.CommitGraphCache;
			if (commitGraphCache == null)
			{
				return;
			}
			LocalBranch localMain = repositoryData.References.LocalMain(gitModule);
			if (localMain == null)
			{
				return;
			}
			RemoteBranch remoteBranch = repositoryData.References.Upstream(localMain);
			if (remoteBranch == null)
			{
				return;
			}
			LocalBranch activeBranch = repositoryData.References.ActiveBranch;
			if (activeBranch == null)
			{
				return;
			}
			Branch mainBranch = repositoryData.References.MainBranch(gitModule, commitGraphCache);
			if (mainBranch == null)
			{
				return;
			}
			if (activeBranch != localMain)
			{
				GitCommandResult<BehindAheadCount> gitCommandResult = new GetBehindAheadCountGitCommand().Execute(gitModule, localMain.Sha, remoteBranch.Sha, commitGraphCache);
				if (!gitCommandResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, gitCommandResult.Error).ShowDialog();
					return;
				}
				if (!gitCommandResult.Result.AreInSync())
				{
				new ErrorWindow(string.Format(Translate("'{0}' is not in sync with '{1}'. You must checkout and sync '{0}' first."), localMain.Name, remoteBranch.Name)).ShowDialog();
					return;
				}
			}
			SubmodulesToUpdate submodulesToUpdate = repositoryUserControl.SubmodulesToUpdate();
			repositoryUserControl.JobQueue.Add(string.Format(Translate("Sync '{0}' with '{1}'"), activeBranch.Name, mainBranch.Name), delegate(JobMonitor monitor)
			{
				string upstreamFullReference = repositoryData.References.Upstream(activeBranch)?.FullReference;
				GitCommandResult startSyncResult = LeanBranching.StartSync(gitModule, localMain.FullReference, mainBranch.FullReference, activeBranch.FullReference, activeBranch.Sha.ToString(), upstreamFullReference, submodulesToUpdate, repositoryStatus.WorkingDirectoryIsDirty(), monitor);
				if (!startSyncResult.Succeeded)
				{
					repositoryUserControl.Dispatcher.Async(delegate
					{
						new ErrorWindow(repositoryUserControl, startSyncResult.Error).ShowDialog();
						repositoryUserControl.InvalidateAndRefresh(SubDomain.All);
					});
				}
				else
				{
					while (LeanBranching.IsSyncInProgress(gitModule))
					{
						GitCommandResult syncStepResult = LeanBranching.NextSyncStep(gitModule, commitGraphCache, submodulesToUpdate, monitor);
						if (!syncStepResult.Succeeded)
						{
							repositoryUserControl.Dispatcher.Async(delegate
							{
								new ErrorWindow(repositoryUserControl, syncStepResult.Error).ShowDialog();
								repositoryUserControl.InvalidateAndRefresh(SubDomain.All);
							});
							return;
						}
					}
					repositoryUserControl.Dispatcher.Async(delegate
					{
						repositoryUserControl.InvalidateAndRefresh(SubDomain.All);
					});
				}
			}, JobFlags.SaveToLog);
		}

		public void Continue(RepositoryUserControl repositoryUserControl)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null || !LeanBranching.IsSyncInProgress(gitModule))
			{
				return;
			}
			CommitGraphCache commitGraphCache = repositoryUserControl.CommitGraphCache;
			if (commitGraphCache == null)
			{
				return;
			}
			SubmodulesToUpdate submodulesToUpdate = repositoryUserControl.SubmodulesToUpdate();
			repositoryUserControl.JobQueue.Add(Translate("Continue Sync"), delegate(JobMonitor monitor)
			{
				while (LeanBranching.IsSyncInProgress(gitModule))
				{
					GitCommandResult syncStepResult = LeanBranching.NextSyncStep(gitModule, commitGraphCache, submodulesToUpdate, monitor);
					if (!syncStepResult.Succeeded)
					{
						repositoryUserControl.Dispatcher.Async(delegate
						{
							new ErrorWindow(repositoryUserControl, syncStepResult.Error).ShowDialog();
							repositoryUserControl.InvalidateAndRefresh(SubDomain.All);
						});
						return;
					}
				}
				repositoryUserControl.Dispatcher.Async(delegate
				{
					repositoryUserControl.InvalidateAndRefresh(SubDomain.All);
				});
			}, JobFlags.SaveToLog);
		}

		public GitCommandResult Continue(GitModule gitModule, SubmodulesToUpdate submodulesToUpdate, CommitGraphCache commitGraphCache, JobMonitor monitor)
		{
			while (LeanBranching.IsSyncInProgress(gitModule))
			{
				GitCommandResult gitCommandResult = LeanBranching.NextSyncStep(gitModule, commitGraphCache, submodulesToUpdate, monitor);
				if (!gitCommandResult.Succeeded)
				{
					return gitCommandResult;
				}
			}
			return GitCommandResult.Success();
		}

		private static string Translate(string text)
		{
			return ServiceLocator.Localization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
