using System;
using System.Collections.Generic;
using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class ToggleFileStageCommand : IUICommand, IForkPlusCommand
	{
		private const int OptimisticStatusUpdateThreshold = 1000;

		public string Title => "Stage/Unstage File";

		public virtual KeyGesture Shortcut { get; } = new KeyGesture(Key.Return);


		public virtual KeyGesture SecondaryShortcut { get; } = new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift);


		public void Execute(CommitUserControl commitUserControl, RepositoryUserControl repositoryUserControl, ChangedFile[] changedFiles, bool amend)
		{
			if (AreStaged(changedFiles))
			{
				Unstage(commitUserControl, repositoryUserControl, changedFiles, amend);
			}
			else
			{
				Stage(commitUserControl, repositoryUserControl, changedFiles);
			}
		}

		private static bool AreStaged(ChangedFile[] changedFiles)
		{
			foreach (ChangedFile changedFile in changedFiles)
			{
				if (!changedFile.IsDirectory)
				{
					return changedFile.Staged;
				}
			}
			return false;
		}

		private static void Stage(CommitUserControl commitUserControl, RepositoryUserControl repositoryUserControl, ChangedFile[] changedFiles)
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
			if (repositoryStatus == null || commitUserControl.StageJob != null)
			{
				return;
			}
			bool showIgnoredFiles = commitUserControl.ShowIgnoredFiles;
			int fileCount = changedFiles.Length;
			string stageName = (fileCount == 1)
				? ServiceLocator.Localization.FormatCurrent("Stage {0} File", fileCount)
				: ServiceLocator.Localization.FormatCurrent("Stage {0} Files", fileCount);
			// v3.4.0 Layer 2：stage 走 AddUndoable，操作前抓工作区快照（stash create），
			// Undo 时 stash apply --index 恢复 stage 前的 index 状态
			commitUserControl.StageJob = repositoryUserControl.AddUndoable(stageName, delegate(JobMonitor monitor)
			{
				GitCommandResult stageResult = new StageFileGitCommand().Execute(gitModule, changedFiles, monitor);
				if (!stageResult.Succeeded)
				{
					commitUserControl.Dispatcher.Async(delegate
					{
						if (!monitor.IsCanceled)
						{
							commitUserControl.StageJob = null;
							commitUserControl.RefreshStageControls();
							commitUserControl.UpdateCommitSection(updateWarningMessage: false);
							new ErrorWindow(repositoryUserControl, stageResult.Error).ShowDialog();
						}
					});
				}
				else if (TryCreateOptimisticRepositoryStatus(repositoryStatus, changedFiles, stage: true, out var optimisticRepositoryStatus))
				{
					commitUserControl.Dispatcher.Async(delegate
					{
						if (!monitor.IsCanceled)
						{
							commitUserControl.StageJob = null;
							repositoryUserControl.UpdateRepositoryStatus(optimisticRepositoryStatus);
							commitUserControl.RefreshStageControls();
							commitUserControl.UpdateCommitSection(updateWarningMessage: false);
							RefreshStatusInBackground(repositoryUserControl);
						}
					});
				}
				else if (ExceedLength(changedFiles) || changedFiles.ContainsItem((ChangedFile x) => x.ChangeType == ChangeType.Added || x.ChangeType == ChangeType.Deleted || x.ChangeType == ChangeType.Unmerged))
				{
					commitUserControl.Dispatcher.Async(delegate
					{
						if (!monitor.IsCanceled)
						{
							commitUserControl.StageJob = null;
							commitUserControl.RefreshStageControls();
							commitUserControl.UpdateCommitSection(updateWarningMessage: false);
							SubDomain subdomains2 = SubDomain.Status;
							repositoryUserControl.InvalidateAndRefresh(subdomains2, null, RepositoryViewMode.CommitViewMode);
						}
					});
				}
				else
				{
					string[] pathsToRefresh = changedFiles.Map((ChangedFile x) => x.Path);
					GitCommandResult<RepositoryStatus> refreshFileResponse = new RefreshFileStatusCommand().Execute(gitModule, repositoryData, repositoryStatus, pathsToRefresh, showIgnoredFiles, monitor);
					commitUserControl.Dispatcher.Async(delegate
					{
						if (!monitor.IsCanceled)
						{
							commitUserControl.StageJob = null;
							commitUserControl.RefreshStageControls();
							commitUserControl.UpdateCommitSection(updateWarningMessage: false);
							if (!refreshFileResponse.Succeeded)
							{
								new ErrorWindow(repositoryUserControl, refreshFileResponse.Error).ShowDialog();
								SubDomain subdomains = SubDomain.Status;
								repositoryUserControl.InvalidateAndRefresh(subdomains, null, RepositoryViewMode.CommitViewMode);
							}
							else
							{
								repositoryUserControl.UpdateRepositoryStatus(refreshFileResponse.Result);
							}
						}
					});
				}
				return stageResult;
			}, JobFlags.SaveToLog | JobFlags.ShowOnToolbar);
			commitUserControl.RefreshStageControls();
			commitUserControl.UpdateCommitSection(updateWarningMessage: false);
		}

		private static void Unstage(CommitUserControl commitUserControl, RepositoryUserControl repositoryUserControl, ChangedFile[] changedFiles, bool amend)
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
			if (repositoryStatus == null || commitUserControl.StageJob != null)
			{
				return;
			}
			bool amendMode = amend;
			bool showIgnoredFiles = commitUserControl.ShowIgnoredFiles;
			int fileCount = changedFiles.Length;
			string unstageName = (fileCount == 1)
				? ServiceLocator.Localization.FormatCurrent("Unstage {0} File", fileCount)
				: ServiceLocator.Localization.FormatCurrent("Unstage {0} Files", fileCount);
			// v3.4.0 Layer 2：unstage 走 AddUndoable，操作前抓工作区快照（stash create），
			// Undo 时 stash apply --index 恢复 unstage 前的 index 状态
			commitUserControl.StageJob = repositoryUserControl.AddUndoable(unstageName, delegate(JobMonitor monitor)
			{
				GitCommandResult unstageResult = (amendMode ? new UnstageForAmendGitCommand().Execute(gitModule, changedFiles, monitor) : new UnstageGitCommand().Execute(gitModule, changedFiles, monitor));
				if (!unstageResult.Succeeded)
				{
					commitUserControl.Dispatcher.Async(delegate
					{
						if (!monitor.IsCanceled)
						{
							commitUserControl.StageJob = null;
							commitUserControl.RefreshStageControls();
							commitUserControl.UpdateCommitSection(updateWarningMessage: false);
							new ErrorWindow(repositoryUserControl, unstageResult.Error).ShowDialog();
						}
					});
				}
				else if (!amendMode && TryCreateOptimisticRepositoryStatus(repositoryStatus, changedFiles, stage: false, out var optimisticRepositoryStatus))
				{
					commitUserControl.Dispatcher.Async(delegate
					{
						if (!monitor.IsCanceled)
						{
							commitUserControl.StageJob = null;
							repositoryUserControl.UpdateRepositoryStatus(optimisticRepositoryStatus);
							commitUserControl.RefreshStageControls();
							commitUserControl.UpdateCommitSection(updateWarningMessage: false);
							RefreshStatusInBackground(repositoryUserControl);
						}
					});
				}
				else if (ExceedLength(changedFiles))
				{
					commitUserControl.Dispatcher.Async(delegate
					{
						if (!monitor.IsCanceled)
						{
							commitUserControl.StageJob = null;
							commitUserControl.RefreshStageControls();
							commitUserControl.UpdateCommitSection(updateWarningMessage: false);
							SubDomain subdomains2 = SubDomain.Status;
							repositoryUserControl.InvalidateAndRefresh(subdomains2, null, RepositoryViewMode.CommitViewMode);
						}
					});
				}
				else
				{
					List<string> list = new List<string>(changedFiles.Length);
					ChangedFile[] array = changedFiles;
					foreach (ChangedFile changedFile in array)
					{
						if (changedFile.ChangeType == ChangeType.Renamed)
						{
							list.Add(changedFile.OldPath);
						}
						list.Add(changedFile.Path);
					}
					GitCommandResult<RepositoryStatus> refreshFileResponse = new RefreshFileStatusCommand().Execute(gitModule, repositoryData, repositoryStatus, list.ToArray(), showIgnoredFiles, monitor);
					commitUserControl.Dispatcher.Async(delegate
					{
						if (!monitor.IsCanceled)
						{
							commitUserControl.StageJob = null;
							commitUserControl.RefreshStageControls();
							commitUserControl.UpdateCommitSection(updateWarningMessage: false);
							if (!refreshFileResponse.Succeeded)
							{
								new ErrorWindow(repositoryUserControl, refreshFileResponse.Error).ShowDialog();
								SubDomain subdomains = SubDomain.Status;
								repositoryUserControl.InvalidateAndRefresh(subdomains, null, RepositoryViewMode.CommitViewMode);
							}
							else
							{
								repositoryUserControl.UpdateRepositoryStatus(refreshFileResponse.Result);
							}
						}
					});
				}
				return unstageResult;
			}, JobFlags.SaveToLog | JobFlags.ShowOnToolbar);
			commitUserControl.RefreshStageControls();
			commitUserControl.UpdateCommitSection(updateWarningMessage: false);
		}

		private static bool ExceedLength(ChangedFile[] changedFiles)
		{
			int num = 0;
			foreach (ChangedFile changedFile in changedFiles)
			{
				num += changedFile.Path.Length;
				if (num > Consts.Env.ArgumentLengthLimit)
				{
					return true;
				}
			}
			return false;
		}

		private static bool TryCreateOptimisticRepositoryStatus(RepositoryStatus repositoryStatus, ChangedFile[] changedFiles, bool stage, out RepositoryStatus optimisticRepositoryStatus)
		{
			optimisticRepositoryStatus = null;
			if (repositoryStatus == null || changedFiles.Length < OptimisticStatusUpdateThreshold || HasUnsupportedOptimisticChange(changedFiles))
			{
				return false;
			}
			HashSet<string> selectedPaths = new HashSet<string>(StringComparer.Ordinal);
			foreach (ChangedFile changedFile in changedFiles)
			{
				if (!changedFile.IsDirectory)
				{
					selectedPaths.Add(changedFile.Path);
				}
			}
			if (selectedPaths.Count == 0)
			{
				return false;
			}
			List<ChangedFile> result = new List<ChangedFile>(repositoryStatus.ChangedFiles.Length + selectedPaths.Count);
			HashSet<string> retainedUnstagedPaths = new HashSet<string>(StringComparer.Ordinal);
			foreach (ChangedFile changedFile in repositoryStatus.ChangedFiles)
			{
				if (!selectedPaths.Contains(changedFile.Path))
				{
					result.Add(changedFile);
					if (!changedFile.Staged)
					{
						retainedUnstagedPaths.Add(changedFile.Path);
					}
				}
				else if (!stage && !changedFile.Staged)
				{
					result.Add(changedFile);
					retainedUnstagedPaths.Add(changedFile.Path);
				}
			}
			foreach (ChangedFile changedFile in changedFiles)
			{
				if (changedFile.IsDirectory)
				{
					continue;
				}
				ChangedFile converted = stage ? ToStagedChangedFile(changedFile) : ToUnstagedChangedFile(changedFile, retainedUnstagedPaths);
				if (converted != null)
				{
					result.Add(converted);
				}
			}
			result.Sort(ChangedFile.Comparer);
			optimisticRepositoryStatus = new RepositoryStatus(repositoryStatus.RepositoryState, CountDistinctChangedFiles(result), result.ToArray());
			return true;
		}

		private static bool HasUnsupportedOptimisticChange(ChangedFile[] changedFiles)
		{
			foreach (ChangedFile changedFile in changedFiles)
			{
				if (changedFile is SubmoduleChangedFile || changedFile.ChangeType == ChangeType.Unmerged || changedFile.ChangeType == ChangeType.Renamed || changedFile.ChangeType == ChangeType.Copied || changedFile.ChangeType == ChangeType.Ignored || changedFile.ChangeType == ChangeType.Unknown)
				{
					return true;
				}
			}
			return false;
		}

		private static ChangedFile ToStagedChangedFile(ChangedFile changedFile)
		{
			StatusType status = ToStatusType(changedFile.ChangeType);
			return new ChangedFile(changedFile.Path, status, StatusType.None, changedFile.OldPath, changedFile.TreeIsh, changedFile.FileMode);
		}

		private static ChangedFile ToUnstagedChangedFile(ChangedFile changedFile, HashSet<string> retainedUnstagedPaths)
		{
			if (retainedUnstagedPaths.Contains(changedFile.Path))
			{
				return null;
			}
			if (changedFile.ChangeType == ChangeType.Added)
			{
				return new ChangedFile(changedFile.Path, StatusType.Untracked, StatusType.Untracked, changedFile.OldPath, changedFile.TreeIsh, changedFile.FileMode);
			}
			StatusType workingDirectoryStatus = ToStatusType(changedFile.ChangeType);
			return new ChangedFile(changedFile.Path, StatusType.None, workingDirectoryStatus, changedFile.OldPath, changedFile.TreeIsh, changedFile.FileMode);
		}

		private static StatusType ToStatusType(ChangeType changeType)
		{
			switch (changeType)
			{
			case ChangeType.Modified:
				return StatusType.Modified;
			case ChangeType.Deleted:
				return StatusType.Deleted;
			case ChangeType.Added:
				return StatusType.Added;
			case ChangeType.TypeChanged:
				return StatusType.TypeChanged;
			default:
				return StatusType.Unknown;
			}
		}

		private static int CountDistinctChangedFiles(List<ChangedFile> changedFiles)
		{
			int count = 0;
			string lastPath = null;
			foreach (ChangedFile changedFile in changedFiles)
			{
				if (changedFile.Path != lastPath)
				{
					count++;
					lastPath = changedFile.Path;
				}
			}
			return count;
		}

		private static void RefreshStatusInBackground(RepositoryUserControl repositoryUserControl)
		{
			repositoryUserControl.InvalidateAndRefresh(SubDomain.Status, null, RepositoryViewMode.CommitViewMode);
		}
	}
}
