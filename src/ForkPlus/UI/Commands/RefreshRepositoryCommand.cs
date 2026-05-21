using System.Collections.Generic;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Commands
{
	internal class RefreshRepositoryCommand
	{
		[Null]
		private Job _activeRefreshRepositoryJob;

		public void Execute(RepositoryUserControl repositoryUserControl, IReadOnlyList<Sha> requiredShas, [Null] RevisionSelector select = null, RepositoryViewMode priority = RepositoryViewMode.RevisionViewMode)
		{
			RepositoryData oldRepositoryData = repositoryUserControl.RepositoryData;
			RepositoryStatus oldRepositoryStatus = repositoryUserControl.RepositoryStatus;
			GitModule gitModule = repositoryUserControl.GitModule;
			CommitGraphCache commitGraphCache = repositoryUserControl.CommitGraphCache;
			bool showReflogInRevisionList = repositoryUserControl.ShowReflogInRevisionList;
			bool showIgnoredFiles = repositoryUserControl.Content.CommitUserControl.ShowIgnoredFiles;
			bool hideUntrackedFiles = gitModule.Settings.HideUntrackedFiles;
			SubDomain subdomainsToReload = repositoryUserControl.InvalidatedSubdomains;
			_activeRefreshRepositoryJob?.Monitor.Cancel();
			Job activeRefreshRepositoryJob = repositoryUserControl.JobQueue.Add(PreferencesLocalization.Current("Refresh Repository"), delegate(JobMonitor monitor)
			{
				if (!monitor.IsCanceled)
				{
					bool repositoryDataChanged;
					if (priority == RepositoryViewMode.RevisionViewMode)
					{
						RepositoryData repositoryData = RefreshRepositoryData(repositoryUserControl, gitModule, oldRepositoryData, showReflogInRevisionList, requiredShas, subdomainsToReload, select, commitGraphCache, monitor, out repositoryDataChanged);
						RefreshRepositoryStatus(repositoryUserControl, gitModule, oldRepositoryStatus, repositoryData, hideUntrackedFiles, showIgnoredFiles, subdomainsToReload, monitor);
					}
					else
					{
						RefreshRepositoryStatus(repositoryUserControl, gitModule, oldRepositoryStatus, oldRepositoryData, hideUntrackedFiles, showIgnoredFiles, subdomainsToReload, monitor);
						RefreshRepositoryData(repositoryUserControl, gitModule, oldRepositoryData, showReflogInRevisionList, requiredShas, subdomainsToReload, select, commitGraphCache, monitor, out repositoryDataChanged);
					}
					repositoryUserControl.Dispatcher.Async(delegate
					{
						_activeRefreshRepositoryJob = null;
					});
				}
			}, JobFlags.Hidden);
			_activeRefreshRepositoryJob = activeRefreshRepositoryJob;
		}

		[Null]
		private RepositoryData RefreshRepositoryData(RepositoryUserControl repositoryUserControl, GitModule gitModule, RepositoryData oldRepositoryData, bool showReflogInRevisionList, IReadOnlyList<Sha> requiredShas, SubDomain subdomainsToReload, [Null] RevisionSelector select, CommitGraphCache commitGraphCache, JobMonitor cancellationToken, out bool repositoryDataChanged)
		{
			repositoryDataChanged = false;
			if ((subdomainsToReload & SubDomain.RepositoryData) == 0)
			{
				return oldRepositoryData;
			}
			if (cancellationToken.IsCanceled)
			{
				return oldRepositoryData;
			}
			Log.Info("RefreshRepositoryData");
			GitCommandResult<RepositoryData> response = new RefreshRepositoryDataGitCommand().Execute(gitModule, showReflogInRevisionList, oldRepositoryData, requiredShas, subdomainsToReload, cancellationToken, commitGraphCache);
			if (cancellationToken.IsCanceled)
			{
				return oldRepositoryData;
			}
			if (!response.Succeeded)
			{
				Log.Warn($"Refresh repository data failed: {response.Error}");
				repositoryUserControl.Dispatcher.Async(delegate
				{
					new ErrorWindow(repositoryUserControl, response.Error).ShowDialog();
				});
				return null;
			}
			RepositoryData newRepositoryData = response.Result;
			if (oldRepositoryData == newRepositoryData)
			{
				repositoryUserControl.Dispatcher.Async(delegate
				{
					repositoryUserControl.ResetSubdomains(SubDomain.RepositoryData);
				});
				return oldRepositoryData;
			}
			Log.Info("Refresh '" + repositoryUserControl.RepositoryName + "' data. Updated.");
			repositoryDataChanged = true;
			repositoryUserControl.Dispatcher.Async(delegate
			{
				repositoryUserControl.UpdateRepositoryData(newRepositoryData, null, select);
				repositoryUserControl.ResetSubdomains(SubDomain.RepositoryData);
			});
			return newRepositoryData;
		}

		private void RefreshRepositoryStatus(RepositoryUserControl repositoryUserControl, GitModule gitModule, RepositoryStatus oldRepositoryStatus, RepositoryData repositoryData, bool hideUntrackedFiles, bool showIgnoredFiles, SubDomain subdomainsToReload, JobMonitor cancellationToken)
		{
			if ((subdomainsToReload & SubDomain.Status) == 0 || cancellationToken.IsCanceled)
			{
				return;
			}
			Log.Info("RefreshRepositoryStatus");
			GitCommandResult<RepositoryStatus> gitCommandResult = new GetRepositoryStatusGitCommand().Execute(gitModule, repositoryData?.Submodules.Items, oldRepositoryStatus, hideUntrackedFiles, showIgnoredFiles, subdomainsToReload, cancellationToken);
			if (cancellationToken.IsCanceled)
			{
				return;
			}
			if (!gitCommandResult.Succeeded)
			{
				Log.Warn($"Refreshing repository status failed: {gitCommandResult.Error}");
				return;
			}
			RepositoryStatus newRepositoryStatus = gitCommandResult.Result;
			Log.Info($"Refresh '{repositoryUserControl.RepositoryName}' status. Updated {newRepositoryStatus.ChangedFiles.Length} files");
			repositoryUserControl.Dispatcher.Async(delegate
			{
				repositoryUserControl.UpdateRepositoryStatus(newRepositoryStatus);
				repositoryUserControl.ResetSubdomains(SubDomain.Status);
			});
		}
	}
}
