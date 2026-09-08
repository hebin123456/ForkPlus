using System.Collections.Generic;
using System.Text;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.Git.Commands
{
	public class UnstageGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, ChangedFile[] files, JobMonitor monitor)
		{
			if (files.Length < 1)
			{
				return GitCommandResult.Success();
			}
			List<string> list = new List<string>();
			List<string> list2 = new List<string>();
			foreach (ChangedFile changedFile in files)
			{
				if (changedFile.ChangeType == ChangeType.Added)
				{
					list.Add(changedFile.Path);
					continue;
				}
				list2.Add(changedFile.Path);
				if (changedFile.OldPath != null && (changedFile.ChangeType == ChangeType.Renamed || changedFile.ChangeType == ChangeType.Copied))
				{
					list2.Add(changedFile.OldPath);
				}
			}
			ProcessOutputHandler processOutputHandler = new ProcessOutputHandler(monitor);
			if (list.Count > 0)
			{
				byte[] bytes = Encoding.UTF8.GetBytes(string.Join("\0", list));
				// 问题6：写入侧四件套对齐（见 ReliableGitFlags）——unstage 与 stage 同族。
				GitCommand removeCommand = new GitCommand(ReliableGitFlags.Prefix, "update-index", "--force-remove", "-z", "--stdin");
				ExecuteWithCallbackResponse executeWithCallbackResponse = new GitRequest(gitModule).Command(removeCommand).Stdin(bytes).ExecuteWithCallbackBt(processOutputHandler.StdoutHandler, processOutputHandler.StderrHandler, retryIfLocked: true, monitor);
				if (monitor.IsCanceled)
				{
					return GitCommandResult.Failure(new GitCommandError.Cancelled());
				}
				ISpawnError error = executeWithCallbackResponse.Error;
				if (error != null)
				{
					monitor.Fail(PreferencesLocalization.Current("unstage failed"));
					return GitCommandResult.Failure(error.ToGitCommandError());
				}
				if (!executeWithCallbackResponse.Result.Success)
				{
					monitor.Fail(PreferencesLocalization.Current("unstage failed"));
					if (GitCommandError.RepositoryIsLocked.Test(processOutputHandler.Stderr()))
					{
						return GitCommandResult.Failure(new GitCommandError.RepositoryIsLocked(processOutputHandler.FullOutput(), processOutputHandler.Stderr()));
					}
					return GitCommandResult.Failure(new GitCommandError.GitError(processOutputHandler.FullOutput(), processOutputHandler.Stderr()));
				}
			}
			if (list2.Count > 0)
			{
				byte[] bytes2 = Encoding.UTF8.GetBytes(string.Join("\0", list2));
				// 问题6：写入侧四件套对齐（见 ReliableGitFlags）。
				GitCommand resetCommand = new GitCommand(ReliableGitFlags.Prefix, "reset", "HEAD", "--pathspec-from-file=-", "--pathspec-file-nul", "--");
				ExecuteWithCallbackResponse executeWithCallbackResponse2 = new GitRequest(gitModule).Command(resetCommand).Stdin(bytes2).ExecuteWithCallbackBt(processOutputHandler.StdoutHandler, processOutputHandler.StderrHandler, retryIfLocked: true, monitor);
				if (monitor.IsCanceled)
				{
					return GitCommandResult.Failure(new GitCommandError.Cancelled());
				}
				ISpawnError error2 = executeWithCallbackResponse2.Error;
				if (error2 != null)
				{
					monitor.Fail(PreferencesLocalization.Current("unstage failed"));
					return GitCommandResult.Failure(error2.ToGitCommandError());
				}
				if (!executeWithCallbackResponse2.Result.Success)
				{
					monitor.Fail(PreferencesLocalization.Current("unstage failed"));
					if (GitCommandError.RepositoryIsLocked.Test(processOutputHandler.Stderr()))
					{
						return GitCommandResult.Failure(new GitCommandError.RepositoryIsLocked(processOutputHandler.FullOutput(), processOutputHandler.Stderr()));
					}
					return GitCommandResult.Failure(new GitCommandError.GitError(processOutputHandler.FullOutput(), processOutputHandler.Stderr()));
				}
			}
			monitor.Success(PreferencesLocalization.Current("unstaged"));
			return GitCommandResult.Success();
		}
	}
}
