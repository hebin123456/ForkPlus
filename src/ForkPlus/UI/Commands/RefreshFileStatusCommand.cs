using System;
using System.Collections.Generic;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class RefreshFileStatusCommand
	{
		public void Execute(CommitUserControl commitUserControl, RepositoryUserControl repositoryUserControl, string[] pathsToRefresh)
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
			RepositoryStatus oldRepositoryStatus = repositoryUserControl.RepositoryStatus;
			if (oldRepositoryStatus == null || pathsToRefresh.Length == 0)
			{
				return;
			}
			repositoryUserControl.JobQueue.Add(ServiceLocator.Localization.Current("Refresh working directory"), delegate(JobMonitor monitor)
			{
				GitCommandResult<RepositoryStatus> response = Execute(gitModule, repositoryData, oldRepositoryStatus, pathsToRefresh, commitUserControl.ShowIgnoredFiles, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (!response.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, response.Error).ShowDialog();
						SubDomain subdomains = SubDomain.Status;
						repositoryUserControl.InvalidateAndRefresh(subdomains, null, RepositoryViewMode.CommitViewMode);
					}
					else
					{
						repositoryUserControl.UpdateRepositoryStatus(response.Result);
					}
				});
			}, JobFlags.Hidden, showMessageWhenDone: false);
		}

		public GitCommandResult<RepositoryStatus> Execute(GitModule gitModule, RepositoryData repositoryData, RepositoryStatus repositoryStatus, string[] pathsToRefresh, bool showIgnoredFiles, JobMonitor monitor)
		{
			if (pathsToRefresh.Length == 0)
			{
				return GitCommandResult<RepositoryStatus>.Success(repositoryStatus);
			}
			GitCommandResult<ChangedFilesCollection> gitCommandResult = new GetChangedFilesGitCommand().Execute(gitModule, pathsToRefresh, repositoryData.Submodules.Items, showIgnoredFiles);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<RepositoryStatus>.Failure(gitCommandResult.Error);
			}
			ChangedFilesCollection result = gitCommandResult.Result;
			Consolidate(repositoryStatus.ChangedFiles, pathsToRefresh, result.ChangedFiles, out var resultChangedFiles, out var resultChangedFilesCount, monitor);
			return GitCommandResult<RepositoryStatus>.Success(new RepositoryStatus(repositoryStatus.RepositoryState, resultChangedFilesCount, resultChangedFiles));
		}

		private static void Consolidate(ChangedFile[] old, string[] requestedPaths, ChangedFile[] refreshed, out ChangedFile[] resultChangedFiles, out int resultChangedFilesCount, JobMonitor monitor)
		{
			List<ChangedFile> list = new List<ChangedFile>(Math.Max(old.Length, refreshed.Length));
			monitor.AppendOutputLine("Old: ");
			ChangedFile[] array = old;
			foreach (ChangedFile changedFile in array)
			{
				Trace(monitor, Description(changedFile));
			}
			monitor.AppendOutputLine("\nRequested: ");
			foreach (string message in requestedPaths)
			{
				Trace(monitor, message);
			}
			monitor.AppendOutputLine("\nResponse: ");
			array = refreshed;
			foreach (ChangedFile changedFile2 in array)
			{
				Trace(monitor, Description(changedFile2));
			}
			monitor.AppendOutputLine("\nRefreshed: ");
			int j = 0;
			int k = 0;
			resultChangedFilesCount = 0;
			while (j < old.Length && k < refreshed.Length)
			{
				int num = ChangedFile.Comparer.Compare(old[j], refreshed[k]);
				if (num < 0)
				{
					if (!IsRequestedToRefresh(old[j], requestedPaths))
					{
						list.Add(old[j]);
						CountDistinctChangedFiles(list, ref resultChangedFilesCount);
					}
					else
					{
						Trace(monitor, "- " + Description(old[j]));
					}
					j++;
				}
				else if (num > 0)
				{
					Trace(monitor, "+ " + Description(refreshed[k]));
					list.Add(refreshed[k]);
					CountDistinctChangedFiles(list, ref resultChangedFilesCount);
					k++;
				}
				else
				{
					list.Add(refreshed[k]);
					CountDistinctChangedFiles(list, ref resultChangedFilesCount);
					j++;
					k++;
				}
			}
			for (; j < old.Length; j++)
			{
				if (!IsRequestedToRefresh(old[j], requestedPaths))
				{
					list.Add(old[j]);
					CountDistinctChangedFiles(list, ref resultChangedFilesCount);
				}
				else
				{
					Trace(monitor, "- " + Description(old[j]));
				}
			}
			for (; k < refreshed.Length; k++)
			{
				Trace(monitor, "+ " + Description(refreshed[k]));
				list.Add(refreshed[k]);
				CountDistinctChangedFiles(list, ref resultChangedFilesCount);
			}
			monitor.AppendOutputLine("\nResult: ");
			foreach (ChangedFile item in list)
			{
				Trace(monitor, Description(item));
			}
			resultChangedFiles = list.ToArray();
			Log.Info($"# New Changed Files: {resultChangedFilesCount}");
		}

		private static void Trace(JobMonitor monitor, string message)
		{
			monitor.AppendOutputLine(message);
		}

		private static bool IsRequestedToRefresh(ChangedFile changedFile, string[] requestedPaths)
		{
			return requestedPaths.ContainsItem((string x) => x == changedFile.Path);
		}

		private static void CountDistinctChangedFiles(List<ChangedFile> list, ref int count)
		{
			int count2 = list.Count;
			if (count2 < 2 || !(list[count2 - 1].Path == list[count2 - 2].Path))
			{
				count++;
			}
		}

		private static string Description(ChangedFile changedFile)
		{
			string text = changedFile.ChangeType switch
			{
				ChangeType.Added => "A", 
				ChangeType.Copied => "C", 
				ChangeType.Deleted => "D", 
				ChangeType.Ignored => "I", 
				ChangeType.Modified => "M", 
				ChangeType.Renamed => "R", 
				ChangeType.TypeChanged => "T", 
				ChangeType.Unmerged => "U", 
				_ => "?", 
			};
			if (changedFile.Staged)
			{
				return " " + text + " " + changedFile.Path;
			}
			return text + "  " + changedFile.Path;
		}
	}
}
