using ForkPlus.Services;
using System.Collections.Generic;
using System.Text;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

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
				ExecuteWithCallbackResponse executeWithCallbackResponse = new GitRequest(gitModule).Command("update-index", "--force-remove", "-z", "--stdin").Stdin(bytes).ExecuteWithCallbackBt(processOutputHandler.StdoutHandler, processOutputHandler.StderrHandler, retryIfLocked: true, monitor);
				if (monitor.IsCanceled)
				{
					return GitCommandResult.Failure(new GitCommandError.Cancelled());
				}
				ISpawnError error = executeWithCallbackResponse.Error;
				if (error != null)
				{
					monitor.Fail(ServiceLocator.Localization.Current("unstage failed"));
					return GitCommandResult.Failure(error.ToGitCommandError());
				}
				if (!executeWithCallbackResponse.Result.Success)
				{
					monitor.Fail(ServiceLocator.Localization.Current("unstage failed"));
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
				ExecuteWithCallbackResponse executeWithCallbackResponse2 = new GitRequest(gitModule).Command("reset", "HEAD", "--pathspec-from-file=-", "--pathspec-file-nul", "--").Stdin(bytes2).ExecuteWithCallbackBt(processOutputHandler.StdoutHandler, processOutputHandler.StderrHandler, retryIfLocked: true, monitor);
				if (monitor.IsCanceled)
				{
					return GitCommandResult.Failure(new GitCommandError.Cancelled());
				}
				ISpawnError error2 = executeWithCallbackResponse2.Error;
				if (error2 != null)
				{
					monitor.Fail(ServiceLocator.Localization.Current("unstage failed"));
					return GitCommandResult.Failure(error2.ToGitCommandError());
				}
				if (!executeWithCallbackResponse2.Result.Success)
				{
					monitor.Fail(ServiceLocator.Localization.Current("unstage failed"));
					if (GitCommandError.RepositoryIsLocked.Test(processOutputHandler.Stderr()))
					{
						return GitCommandResult.Failure(new GitCommandError.RepositoryIsLocked(processOutputHandler.FullOutput(), processOutputHandler.Stderr()));
					}
					return GitCommandResult.Failure(new GitCommandError.GitError(processOutputHandler.FullOutput(), processOutputHandler.Stderr()));
				}
			}
			monitor.Success(ServiceLocator.Localization.Current("unstaged"));
			return GitCommandResult.Success();
		}
	}
}
