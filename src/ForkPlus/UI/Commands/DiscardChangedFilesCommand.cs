using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Commands
{
	public class DiscardChangedFilesCommand : IUICommand, IForkPlusCommand
	{
		private class ChangedFileComparer : IEqualityComparer<ChangedFile>
		{
			public bool Equals(ChangedFile x, ChangedFile y)
			{
				return x.Path == y.Path;
			}

			public int GetHashCode(ChangedFile obj)
			{
				return obj.Path.GetHashCode();
			}
		}

		private class SubmoduleComparer : IEqualityComparer<Submodule>
		{
			public bool Equals(Submodule x, Submodule y)
			{
				return x.Path == y.Path;
			}

			public int GetHashCode(Submodule obj)
			{
				return obj.Path.GetHashCode();
			}
		}

		public string Title => "Discard changes...";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.Delete);


		public KeyGesture SecondaryShortcut { get; } = new KeyGesture(Key.D, ModifierKeys.Control | ModifierKeys.Shift);


		public void Execute(CommitUserControl commitUserControl, RepositoryUserControl repositoryUserControl, ChangedFile[] changedFiles)
		{
			if (changedFiles.Length == 0)
			{
				return;
			}
			HashSet<Submodule> hashSet = new HashSet<Submodule>(new SubmoduleComparer());
			HashSet<ChangedFile> hashSet2 = new HashSet<ChangedFile>(new ChangedFileComparer());
			foreach (ChangedFile changedFile in changedFiles)
			{
				if (!changedFile.IsDirectory)
				{
					if (changedFile is SubmoduleChangedFile submoduleChangedFile)
					{
						hashSet.Add(submoduleChangedFile.Submodule);
					}
					else
					{
						hashSet2.Add(changedFile);
					}
				}
			}
			if (hashSet2.Count > 0)
			{
				if (new MessageBoxWindow("Discard changes", "Do you want to discard all your changes in the selected files?", CreateButtonTitle(changedFiles), "Cancel", showCancelButton: true, 550.0).ShowDialog().GetValueOrDefault())
				{
					DiscardFiles(commitUserControl, repositoryUserControl, hashSet2.ToArray(), hashSet.ToArray());
				}
			}
			else if (hashSet.Count > 0)
			{
				new DiscardSubmoduleChangesCommand().Execute(commitUserControl, repositoryUserControl, hashSet.ToArray());
			}
		}

		private void DiscardFiles(CommitUserControl commitUserControl, RepositoryUserControl repositoryUserControl, ChangedFile[] changedFiles, Submodule[] submodules)
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
			// v3.4.0 Layer 2：discard 走 AddUndoable，操作前抓工作区快照（stash create），
			// Undo 时 stash apply --index 恢复被丢弃的变更
			commitUserControl.StageJob = repositoryUserControl.AddUndoable(Translate("Discard Changes"), delegate(JobMonitor monitor)
			{
				GitCommandResult discardResult = new DiscardFileChangesGitCommand().Execute(gitModule, changedFiles.ToArray(), monitor);
				if (!discardResult.Succeeded)
				{
					commitUserControl.Dispatcher.Async(delegate
					{
						if (!monitor.IsCanceled)
						{
							commitUserControl.StageJob = null;
							commitUserControl.RefreshStageControls();
							commitUserControl.UpdateCommitSection(updateWarningMessage: false);
							new ErrorWindow(repositoryUserControl, discardResult.Error).ShowDialog();
						}
					});
				}
				else
				{
					string[] array = changedFiles.Map((ChangedFile x) => x.Path);
					if (ExceedLength(array))
					{
						if (!monitor.IsCanceled)
						{
							commitUserControl.StageJob = null;
							commitUserControl.RefreshStageControls();
							commitUserControl.UpdateCommitSection(updateWarningMessage: false);
							SubDomain subdomains = SubDomain.Status;
							repositoryUserControl.InvalidateAndRefresh(subdomains, null, RepositoryViewMode.CommitViewMode);
						}
					}
					else
					{
						GitCommandResult<RepositoryStatus> refreshFileResponse = new RefreshFileStatusCommand().Execute(gitModule, repositoryData, repositoryStatus, array, showIgnoredFiles, monitor);
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
									SubDomain subdomains2 = SubDomain.Status;
									repositoryUserControl.InvalidateAndRefresh(subdomains2, null, RepositoryViewMode.CommitViewMode);
								}
								else
								{
									repositoryUserControl.UpdateRepositoryStatus(refreshFileResponse.Result);
								}
								new DiscardSubmoduleChangesCommand().Execute(commitUserControl, repositoryUserControl, submodules);
							}
						});
					}
				}
				return discardResult;
			}, JobFlags.SaveToLog | JobFlags.ShowOnToolbar);
			commitUserControl.RefreshStageControls();
			commitUserControl.UpdateCommitSection(updateWarningMessage: false);
		}

		private static string CreateButtonTitle(ChangedFile[] changedFiles)
		{
			HashSet<string> hashSet = new HashSet<string>();
			for (int i = 0; i < changedFiles.Length; i++)
			{
				hashSet.Add(changedFiles[i].Path);
			}
			if (hashSet.Count <= 1)
			{
				return Translate("Discard");
			}
			return string.Format(Translate("Discard Changes in {0} Files"), hashSet.Count);
		}

		private static bool ExceedLength(string[] paths)
		{
			int num = 0;
			foreach (string text in paths)
			{
				num += text.Length + 1;
				if (num > Consts.Env.ArgumentLengthLimit)
				{
					return true;
				}
			}
			return false;
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
