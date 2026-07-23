using System.Windows.Input;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Commands.LeanBranching;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class CommitCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Commit";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.Return, ModifierKeys.Control | ModifierKeys.Shift);


		public KeyGesture SecondaryShortcut => new KeyGesture(Key.Return, ModifierKeys.Control);

		public void Execute(CommitUserControl commitUserControl, bool commitAndPush = false)
		{
			if (!commitUserControl.IsCommitAllowed)
			{
				return;
			}
			RepositoryUserControl repositoryUserControl = commitUserControl.RepositoryUserControl;
			CommitGraphCache commitGraphCache = repositoryUserControl.CommitGraphCache;
			GitModule gitModule = repositoryUserControl.GitModule;
			RepositoryState repositoryState = repositoryUserControl.RepositoryStatus.RepositoryState;
			SubmodulesToUpdate submodulesToUpdate = repositoryUserControl.SubmodulesToUpdate();
			if (repositoryState is RepositoryState.SequencerInProgress)
			{
				commitUserControl.CommittingInProgress = true;
				commitUserControl.UpdateCommitSection();
				repositoryUserControl.JobQueue.Add(ServiceLocator.Localization.Current("Continue Cherry-Pick"), delegate(JobMonitor monitor)
				{
					GitCommandResult result2 = UpdateSubmodulesIfNeeded(new ContinueCherryPickGitCommand().Execute(gitModule), gitModule, submodulesToUpdate, monitor);
					repositoryUserControl.Dispatcher.Async(delegate
					{
						commitUserControl.CommittingInProgress = false;
						commitUserControl.UpdateCommitSection();
						commitUserControl.Refresh(SubDomain.All);
						if (!result2.Succeeded)
						{
							new ErrorWindow(repositoryUserControl, result2.Error).ShowDialog();
						}
					});
				});
				return;
			}
			if (repositoryState is RepositoryState.AmInProgress)
			{
				commitUserControl.CommittingInProgress = true;
				commitUserControl.UpdateCommitSection();
				repositoryUserControl.JobQueue.Add(ServiceLocator.Localization.Current("Continue Am"), delegate(JobMonitor monitor)
				{
					GitCommandResult result = UpdateSubmodulesIfNeeded(new ContinueAmGitCommand().Execute(gitModule), gitModule, submodulesToUpdate, monitor);
					repositoryUserControl.Dispatcher.Async(delegate
					{
						commitUserControl.CommittingInProgress = false;
						commitUserControl.UpdateCommitSection();
						commitUserControl.Refresh(SubDomain.All);
						if (!result.Succeeded)
						{
							new ErrorWindow(repositoryUserControl, result.Error).ShowDialog();
						}
					});
				});
				return;
			}
			if (repositoryState is RepositoryState.RebaseInProgress rebaseInProgress && (rebaseInProgress.AmendSha == null || (commitUserControl.StageFileUserControl.StagedItemsCount == 0 && !commitUserControl.AmendMode)))
			{
				commitUserControl.CommittingInProgress = true;
				commitUserControl.UpdateCommitSection();
				repositoryUserControl.JobQueue.Add(ServiceLocator.Localization.Current("Continue Rebase"), delegate(JobMonitor monitor)
				{
					GitCommandResult continueRebaseResult = new ContinueRebaseGitCommand().Execute(gitModule);
					if (!continueRebaseResult.Succeeded)
					{
						if (submodulesToUpdate.Length > 0)
						{
							new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
						}
						repositoryUserControl.Dispatcher.Async(delegate
						{
							commitUserControl.CommittingInProgress = false;
							commitUserControl.UpdateCommitSection();
							commitUserControl.Refresh(SubDomain.All);
							new ErrorWindow(repositoryUserControl, continueRebaseResult.Error).ShowDialog();
						});
					}
					else if (LeanBranching.IsSyncInProgress(gitModule))
					{
						GitCommandResult leanBranchingSyncResult = RepositoryUserControl.Commands.LeanBranchingSync.Continue(gitModule, submodulesToUpdate, commitGraphCache, monitor);
						repositoryUserControl.Dispatcher.Async(delegate
						{
							commitUserControl.CommittingInProgress = false;
							commitUserControl.UpdateCommitSection();
							commitUserControl.Refresh(SubDomain.All);
							if (!leanBranchingSyncResult.Succeeded)
							{
								new ErrorWindow(repositoryUserControl, leanBranchingSyncResult.Error).ShowDialog();
							}
						});
					}
					else
					{
						GitCommandResult updateSubmodulesResult = GitCommandResult.Success();
						if (submodulesToUpdate.Length > 0)
						{
							updateSubmodulesResult = new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
						}
						repositoryUserControl.Dispatcher.Async(delegate
						{
							commitUserControl.EraseSavedCommitMessage();
							commitUserControl.DontRefreshOnAmend = true;
							commitUserControl.AmendMode = false;
							commitUserControl.DontRefreshOnAmend = false;
							commitUserControl.FullCommitMessage = "";
							commitUserControl.CommittingInProgress = false;
							commitUserControl.UpdateCommitSection();
							commitUserControl.Refresh(SubDomain.All);
							if (!updateSubmodulesResult.Succeeded)
							{
								new ErrorWindow(repositoryUserControl, updateSubmodulesResult.Error).ShowDialog();
							}
						});
					}
				});
				return;
			}
			string message = commitUserControl.FullCommitMessage;
			bool amend = commitUserControl.AmendMode;
			int stagedCount = commitUserControl.StageFileUserControl.StagedItemsCount;
			string name = amend
			 ? ServiceLocator.Localization.Current("Amend")
			 : ((stagedCount == 1)
			  ? ServiceLocator.Localization.FormatCurrent("Commit {0} File", stagedCount)
			  : ServiceLocator.Localization.FormatCurrent("Commit {0} Files", stagedCount));
			commitUserControl.CommittingInProgress = true;
			commitUserControl.UpdateCommitSection();
			repositoryUserControl.AddUndoable(name, delegate(JobMonitor monitor)
		{
		 string monitorMsg = (stagedCount == 1)
		  ? ServiceLocator.Localization.FormatCurrent("{0} file...", stagedCount)
		  : ServiceLocator.Localization.FormatCurrent("{0} files...", stagedCount);
		 monitor.Update(0.0, monitorMsg);
			GitCommandResult gitResult = new CommitGitCommand().Execute(gitModule, message, amend, commitAndPush, monitor);
			repositoryUserControl.Dispatcher.Async(delegate
			{
				commitUserControl.CommittingInProgress = false;
				if (!gitResult.Succeeded)
				{
					commitUserControl.SaveCommitMessage();
					commitUserControl.Refresh(SubDomain.All);
					new ErrorWindow(repositoryUserControl, gitResult.Error).ShowDialog();
				}
				else if (!monitor.IsCanceled)
			{
				// v3.1.1：用户在 commit 过程中按了取消（IsCanceled=true）时不能再调 Success，
				// 否则会把 Canceled 状态覆盖回 Succeeded（虽然 JobMonitor 已加守卫，但这里语义上也不应调）
				monitor.Success(null);
				commitUserControl.EraseSavedCommitMessage();
				commitUserControl.DontRefreshOnAmend = true;
				commitUserControl.AmendMode = false;
				commitUserControl.DontRefreshOnAmend = false;
				commitUserControl.FullCommitMessage = "";
				commitUserControl.Refresh(SubDomain.All);
				if (commitAndPush)
				{
					MainWindow.Commands.QuickPush.Execute(repositoryUserControl);
				}
			}
			});
			return gitResult;
		}, JobFlags.Default, showMessageWhenDone: false);
	}

		private static GitCommandResult UpdateSubmodulesIfNeeded(GitCommandResult prioritizedResult, GitModule gitModule, SubmodulesToUpdate submodules, JobMonitor monitor)
		{
			if (submodules.Length == 0)
			{
				return prioritizedResult;
			}
			GitCommandResult result = new UpdateSubmodulesGitCommand().Execute(gitModule, submodules, monitor);
			if (!prioritizedResult.Succeeded)
			{
				return prioritizedResult;
			}
			return result;
		}
	}
}
