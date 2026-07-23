using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Diff;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class DiscardChunkCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Discard...";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut { get; }

		public void Execute(CommitUserControl commitUserControl, RepositoryUserControl repositoryUserControl, Patch patch, bool editorIsNewOrUntracked)
		{
			byte[] array = patch.CreatePatchData();
			if (array == null)
			{
				return;
			}
			int num = patch.LinesCount();
			if (num != 0)
			{
				string[] paths = patch.Diffs.Map((Diff x) => x.OldFilepath ?? x.NewFilepath);
				string submitTitle = ((num > 1) ? string.Format(Translate("Discard {0} Lines"), num) : Translate("Discard Line"));
				if (new MessageBoxWindow("Discard changes", "Do you want to discard all your changes in the selected lines?", submitTitle, "Cancel", showCancelButton: true, 550.0).ShowDialog().GetValueOrDefault())
				{
					DiscardChunk(commitUserControl, repositoryUserControl, paths, array, editorIsNewOrUntracked);
				}
			}
		}

		private void DiscardChunk(CommitUserControl commitUserControl, RepositoryUserControl repositoryUserControl, string[] paths, byte[] patchData, bool editorIsNewOrUntracked)
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
			commitUserControl.StageJob = repositoryUserControl.JobQueue.Add(Translate("Discard"), delegate(JobMonitor monitor)
			{
				GitCommandResult discardResult = new ApplyWorkingTreeGitCommand().Execute(gitModule, patchData, monitor);
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
					if (editorIsNewOrUntracked)
					{
						commitUserControl.Dispatcher.Async(delegate
						{
							if (!monitor.IsCanceled)
							{
								commitUserControl.StageJob = null;
								commitUserControl.RefreshStageControls();
								commitUserControl.UpdateCommitSection(updateWarningMessage: false);
								repositoryUserControl.InvalidateAndRefresh(SubDomain.ChangedFiles | SubDomain.UntrackedChangedFiles, null, RepositoryViewMode.CommitViewMode);
							}
						});
					}
					GitCommandResult<RepositoryStatus> refreshFileResponse = new RefreshFileStatusCommand().Execute(gitModule, repositoryData, repositoryStatus, paths, showIgnoredFiles, monitor);
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
			}, JobFlags.Hidden);
			commitUserControl.RefreshStageControls();
			commitUserControl.UpdateCommitSection(updateWarningMessage: false);
		}

		private static string Translate(string text)
		{
			return ServiceLocator.Localization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
