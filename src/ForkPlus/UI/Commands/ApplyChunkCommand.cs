using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Diff;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class ApplyChunkCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Apply";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut { get; }

		public void Execute(CommitUserControl commitUserControl, RepositoryUserControl repositoryUserControl, Patch patch, bool staged, bool editorIsNewOrUntracked)
		{
			byte[] array = patch.CreatePatchData();
			if (array != null && patch.LinesCount() != 0)
			{
				string[] paths = patch.Diffs.Map((Diff x) => x.OldFilepath ?? x.NewFilepath);
				ApplyChunk(commitUserControl, repositoryUserControl, paths, array, staged, editorIsNewOrUntracked);
			}
		}

		private void ApplyChunk(CommitUserControl commitUserControl, RepositoryUserControl repositoryUserControl, string[] paths, byte[] patchData, bool staged, bool editorIsNewOrUntracked)
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
			// v3.4.1：状态栏标题国际化（之前是硬编码英文）
			string name = staged ? ServiceLocator.Localization.Current("Unstage") : ServiceLocator.Localization.Current("Stage");
			commitUserControl.StageJob = repositoryUserControl.JobQueue.Add(name, delegate(JobMonitor monitor)
			{
				if (!new ApplyGitCommand().Execute(gitModule, staged, patchData).Succeeded)
				{
					commitUserControl.Dispatcher.Async(delegate
					{
						if (!monitor.IsCanceled)
						{
							commitUserControl.StageJob = null;
							commitUserControl.RefreshStageControls();
							commitUserControl.UpdateCommitSection();
						}
					});
				}
				else if (editorIsNewOrUntracked)
				{
					commitUserControl.Dispatcher.Async(delegate
					{
						if (!monitor.IsCanceled)
						{
							commitUserControl.StageJob = null;
							commitUserControl.RefreshStageControls();
							commitUserControl.UpdateCommitSection();
							repositoryUserControl.InvalidateAndRefresh(SubDomain.ChangedFiles | SubDomain.UntrackedChangedFiles, null, RepositoryViewMode.CommitViewMode);
						}
					});
				}
				else
				{
					GitCommandResult<RepositoryStatus> refreshFileResponse = new RefreshFileStatusCommand().Execute(gitModule, repositoryData, repositoryStatus, paths, showIgnoredFiles, monitor);
					commitUserControl.Dispatcher.Async(delegate
					{
						if (!monitor.IsCanceled)
						{
							commitUserControl.StageJob = null;
							commitUserControl.RefreshStageControls();
							commitUserControl.UpdateCommitSection();
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
			commitUserControl.UpdateCommitSection();
		}
	}
}
