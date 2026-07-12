using System;
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
	public class QuickPullCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Quick Pull";

		public KeyGesture Shortcut => new KeyGesture(Key.L, ModifierKeys.Alt | ModifierKeys.Control | ModifierKeys.Shift);

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl)
		{
			if (!TryQuickPull(repositoryUserControl) && repositoryUserControl.RepositoryData != null)
			{
				new PullWindow(repositoryUserControl, null).ShowDialog();
			}
		}

		private bool TryQuickPull(RepositoryUserControl repositoryUserControl)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule != null)
			{
				RepositoryStatus repositoryStatus = repositoryUserControl.RepositoryStatus;
				if (repositoryStatus != null)
				{
					SubmodulesToUpdate submodulesToUpdate = repositoryUserControl.SubmodulesToUpdate();
					bool workingDirectoryIsDirty = repositoryStatus.WorkingDirectoryIsDirty();
					GitCommandResult<GitConfig> gitCommandResult = new GetGitConfigGitCommand().Execute(gitModule);
					if (!gitCommandResult.Succeeded)
					{
						return false;
					}
					GitConfig result = gitCommandResult.Result;
					GitCommandResult<ReferenceStorage> gitCommandResult2 = new GetReferencesGitCommand().Execute(gitModule, result);
					if (!gitCommandResult2.Succeeded)
					{
						return false;
					}
					ReferenceStorage result2 = gitCommandResult2.Result;
					LocalBranch activeBranch = result2.CreateLocalBranches().FirstItem((LocalBranch x) => x.IsActive);
					if (activeBranch != null)
					{
						RemoteBranch remoteBranch = result2.CreateRemoteBranches().FirstItem((RemoteBranch x) => x.FullReference == activeBranch.UpstreamFullReference);
						if (remoteBranch != null)
						{
							bool rebase = ForkPlusSettings.Default.Pull_Rebase;
							bool allTags = ForkPlusSettings.Default.FetchAllTags;
							bool stashAndReapply = ForkPlusSettings.Default.Pull_StashAndReapply;
							repositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Pull '{0}'", remoteBranch.Name), delegate(JobMonitor monitor)
							{
								GitCommandResult requestResult = PerformPull(gitModule, remoteBranch.Remote, rebase, allTags, stashAndReapply, workingDirectoryIsDirty, submodulesToUpdate, monitor);
								repositoryUserControl.Dispatcher.Invoke(delegate
								{
									if (!requestResult.Succeeded)
									{
										new ErrorWindow(repositoryUserControl, requestResult.Error).ShowDialog();
									}
									repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Revisions | SubDomain.Head | SubDomain.Stashes | SubDomain.References, new RevisionSelector.Head());
								});
							});
							return true;
						}
					}
					return false;
				}
			}
			return false;
		}

		private static GitCommandResult PerformPull(GitModule gitModule, string remote, bool rebase, bool allTags, bool stashAndReapply, bool workingDirectoryIsDirty, SubmodulesToUpdate submodulesToUpdate, JobMonitor monitor)
		{
			if (stashAndReapply && workingDirectoryIsDirty)
			{
				monitor.Update(10.0, "Stashing...");
				GitCommandResult<bool> gitCommandResult = new SaveStashGitCommand().Execute(gitModule, $"Pull autostash {DateTime.Now}", stageNewFiles: false, monitor);
				if (!gitCommandResult.Succeeded)
				{
					return GitCommandResult.Failure(gitCommandResult.Error);
				}
			}
			GitCommandResult gitCommandResult2 = new PullGitCommand().Execute(gitModule, remote, null, rebase, allTags, monitor);
			if (!gitCommandResult2.Succeeded && !monitor.IsCanceled)
			{
				if (submodulesToUpdate.Length > 0)
				{
					monitor.Update(0.0, "Updating submodules...");
					new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
				}
				monitor.Fail(PreferencesLocalization.Current("Pull failed"));
				return gitCommandResult2;
			}
			GitCommandResult gitCommandResult3 = GitCommandResult.Success();
			if (stashAndReapply && workingDirectoryIsDirty)
			{
				monitor.Update(10.0, "Applying stash...");
				gitCommandResult3 = new ApplyStashGitCommand().Execute(gitModule, "stash@{0}", deleteAfterApply: true, monitor);
			}
			GitCommandResult gitCommandResult4 = GitCommandResult.Success();
			if (submodulesToUpdate.Length > 0)
			{
				monitor.Update(0.0, "Updating submodules...");
				gitCommandResult4 = new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
			}
			if (!gitCommandResult3.Succeeded)
			{
				monitor.Fail(PreferencesLocalization.Current("Apply stash failed"));
				return gitCommandResult3;
			}
			if (!gitCommandResult4.Succeeded)
			{
				monitor.Fail(PreferencesLocalization.Current("Update submodules failed"));
				return gitCommandResult4;
			}
			monitor.Success("Everything is up to date");
			return gitCommandResult2;
		}
	}
}
