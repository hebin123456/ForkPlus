using System;
using System.Collections.Generic;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class GetRepositoryStatusGitCommand
	{
		public GitCommandResult<RepositoryStatus> Execute(GitModule gitModule, Submodule[] submodules, RepositoryStatus oldRepositoryStatus, bool excludeUntrackedFiles, bool includeIgnoredFiles, SubDomain subdomainsToReload, JobMonitor cancellationToken)
		{
			Benchmarker benchmarker = new Benchmarker("GetRepositoryStatusGitCommand");
			if (oldRepositoryStatus != null && (subdomainsToReload & SubDomain.UntrackedChangedFiles) == 0)
			{
				GitCommandResult<ChangedFilesCollection> gitCommandResult = new GetChangedFilesGitCommand().Execute(gitModule, submodules, excludeUntrackedFiles: true, includeIgnoredFiles);
				if (!gitCommandResult.Succeeded)
				{
					return GitCommandResult<RepositoryStatus>.Failure(gitCommandResult.Error);
				}
				TrackChanges(oldRepositoryStatus.ChangedFiles, gitCommandResult.Result.ChangedFiles, out var resultChangedFiles, out var resultChangedFilesCount);
				RepositoryStatus result = new RepositoryStatus(oldRepositoryStatus.RepositoryState, resultChangedFilesCount, resultChangedFiles);
				benchmarker.ReportElapsed();
				return GitCommandResult<RepositoryStatus>.Success(result);
			}
			GitCommandResult<ChangedFilesCollection> gitCommandResult2 = new GetChangedFilesGitCommand().Execute(gitModule, submodules, excludeUntrackedFiles, includeIgnoredFiles);
			if (!gitCommandResult2.Succeeded)
			{
				return GitCommandResult<RepositoryStatus>.Failure(gitCommandResult2.Error);
			}
			if (cancellationToken.IsCanceled)
			{
				return GitCommandResult<RepositoryStatus>.Failure(new GitCommandError.Cancelled());
			}
			RepositoryState repositoryState;
			if (oldRepositoryStatus == null || (subdomainsToReload & SubDomain.State) != 0)
			{
				GitCommandResult<RepositoryState> gitCommandResult3 = new GetRepositoryStateGitCommand().Execute(gitModule, gitCommandResult2.Result.ChangedFiles);
				if (!gitCommandResult3.Succeeded)
				{
					return GitCommandResult<RepositoryStatus>.Failure(gitCommandResult3.Error);
				}
				repositoryState = gitCommandResult3.Result;
			}
			else
			{
				repositoryState = oldRepositoryStatus.RepositoryState;
			}
			if (cancellationToken.IsCanceled)
			{
				return GitCommandResult<RepositoryStatus>.Failure(new GitCommandError.Cancelled());
			}
			RepositoryStatus result2 = new RepositoryStatus(repositoryState, gitCommandResult2.Result.FilesCount, gitCommandResult2.Result.ChangedFiles);
			benchmarker.ReportElapsed();
			return GitCommandResult<RepositoryStatus>.Success(result2);
		}

		private void TrackChanges(ChangedFile[] oldChangedFiles, ChangedFile[] newChangedFiles, out ChangedFile[] resultChangedFiles, out int resultChangedFilesCount)
		{
			List<ChangedFile> list = new List<ChangedFile>(Math.Max(oldChangedFiles.Length, newChangedFiles.Length));
			int i = 0;
			int j = 0;
			resultChangedFilesCount = 0;
			while (i < oldChangedFiles.Length && j < newChangedFiles.Length)
			{
				int num = ChangedFile.Comparer.Compare(oldChangedFiles[i], newChangedFiles[j]);
				if (num < 0)
				{
					if (!oldChangedFiles[i].Tracked)
					{
						list.Add(oldChangedFiles[i]);
						CountDistinctChangedFiles(list, ref resultChangedFilesCount);
						Log.Debug("* " + Description(oldChangedFiles[i]));
					}
					else
					{
						Log.Debug("- " + Description(oldChangedFiles[i]));
					}
					i++;
				}
				else if (num > 0)
				{
					Log.Debug("+ " + Description(newChangedFiles[j]));
					list.Add(newChangedFiles[j]);
					CountDistinctChangedFiles(list, ref resultChangedFilesCount);
					j++;
				}
				else
				{
					list.Add(newChangedFiles[j]);
					CountDistinctChangedFiles(list, ref resultChangedFilesCount);
					i++;
					j++;
				}
			}
			for (; i < oldChangedFiles.Length; i++)
			{
				if (!oldChangedFiles[i].Tracked)
				{
					list.Add(oldChangedFiles[i]);
					CountDistinctChangedFiles(list, ref resultChangedFilesCount);
					Log.Debug("* " + Description(oldChangedFiles[i]));
				}
				else
				{
					Log.Debug("- " + Description(oldChangedFiles[i]));
				}
			}
			for (; j < newChangedFiles.Length; j++)
			{
				Log.Debug("+ " + Description(newChangedFiles[j]));
				list.Add(newChangedFiles[j]);
				CountDistinctChangedFiles(list, ref resultChangedFilesCount);
			}
			resultChangedFiles = list.ToArray();
			Log.Info($"# New Changed Files: {resultChangedFilesCount}");
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

		private static bool ChangedFilesAreEqual(ChangedFilesCollection current, ChangedFile[] oldChangedFiles, int oldChangedFilesCount)
		{
			if (current.FilesCount != oldChangedFilesCount)
			{
				Log.Debug("Detected changed changed files.");
				return false;
			}
			if (current.ChangedFiles.Length != oldChangedFiles.Length)
			{
				Log.Debug("Detected changed changed files.");
				return false;
			}
			for (int i = 0; i < current.ChangedFiles.Length; i++)
			{
				if (!current.ChangedFiles[i].ChangedFileEquals(oldChangedFiles[i]))
				{
					Log.Debug("Detected changed changed file.");
					return false;
				}
			}
			return true;
		}

		private static bool RepositoryStatesAreEqual(RepositoryState repositoryState, RepositoryState oldRepositoryState)
		{
			if (repositoryState is RepositoryState.OK && oldRepositoryState is RepositoryState.OK)
			{
				return true;
			}
			if (repositoryState is RepositoryState.MergeInProgress mergeInProgress && oldRepositoryState is RepositoryState.MergeInProgress mergeInProgress2)
			{
				if (mergeInProgress.Local.ReferenceEquals(mergeInProgress2.Local) && mergeInProgress.Remote.ReferenceEquals(mergeInProgress2.Remote))
				{
					return UnmergedFilesAreEqual(mergeInProgress.UnmergedFiles, mergeInProgress2.UnmergedFiles);
				}
				return false;
			}
			if (repositoryState is RepositoryState.CherryPickInProgress cherryPickInProgress && oldRepositoryState is RepositoryState.CherryPickInProgress cherryPickInProgress2)
			{
				if (UnmergedFilesAreEqual(cherryPickInProgress.UnmergedFiles, cherryPickInProgress2.UnmergedFiles) && cherryPickInProgress.Head.ReferenceEquals(cherryPickInProgress2.Head))
				{
					return cherryPickInProgress.CherryPickHead.ReferenceEquals(cherryPickInProgress2.CherryPickHead);
				}
				return false;
			}
			if (repositoryState is RepositoryState.RevertInProgress revertInProgress && oldRepositoryState is RepositoryState.RevertInProgress revertInProgress2)
			{
				if (revertInProgress.RevertHead == revertInProgress2.RevertHead)
				{
					return UnmergedFilesAreEqual(revertInProgress.UnmergedFiles, revertInProgress2.UnmergedFiles);
				}
				return false;
			}
			if (repositoryState is RepositoryState.RebaseInProgress rebaseInProgress && oldRepositoryState is RepositoryState.RebaseInProgress rebaseInProgress2)
			{
				if (rebaseInProgress.Local.ReferenceEquals(rebaseInProgress2.Local) && rebaseInProgress.Remote.ReferenceEquals(rebaseInProgress2.Remote) && rebaseInProgress.Done == rebaseInProgress2.Done && rebaseInProgress.Total == rebaseInProgress2.Total)
				{
					return UnmergedFilesAreEqual(rebaseInProgress.UnmergedFiles, rebaseInProgress2.UnmergedFiles);
				}
				return false;
			}
			if (repositoryState is RepositoryState.SquashInProgress squashInProgress && oldRepositoryState is RepositoryState.SquashInProgress squashInProgress2)
			{
				return UnmergedFilesAreEqual(squashInProgress.UnmergedFiles, squashInProgress2.UnmergedFiles);
			}
			if (repositoryState is RepositoryState.UnmergedIndex unmergedIndex && oldRepositoryState is RepositoryState.UnmergedIndex unmergedIndex2)
			{
				return UnmergedFilesAreEqual(unmergedIndex.UnmergedFiles, unmergedIndex2.UnmergedFiles);
			}
			return false;
		}

		private static bool UnmergedFilesAreEqual(IReadOnlyList<ChangedFile> current, IReadOnlyList<ChangedFile> old)
		{
			if (current == null)
			{
				if (old == null)
				{
					return true;
				}
				return false;
			}
			if (old == null)
			{
				return false;
			}
			if (current.Count != old.Count)
			{
				Log.Debug("Detected change in unmerged files.");
				return false;
			}
			for (int i = 0; i < current.Count; i++)
			{
				if (!current[i].ChangedFileEquals(old[i]))
				{
					Log.Debug("Detected change in unmerged files.");
					return false;
				}
			}
			return true;
		}
	}
}
