using System;
using System.Collections.Generic;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class SaveWorkingDirectoryAsStashGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string stashMessage, bool stageNewFiles, string sourceString, JobMonitor monitor)
		{
			if (stageNewFiles)
			{
				GitCommandResult<ChangedFilesCollection> gitCommandResult = new GetChangedFilesGitCommand().Execute(gitModule);
				if (!gitCommandResult.Succeeded)
				{
					return gitCommandResult.ToGitCommandResult();
				}
				List<ChangedFile> list = gitCommandResult.Result.ChangedFiles.Filter((ChangedFile x) => x.New && !x.Staged);
				if (list.Count > 0)
				{
					monitor.Update(0.0, $"Staging {list.Count} files...");
				}
				GitCommandResult gitCommandResult2 = new StageFileGitCommand().Execute(gitModule, list.ToArray(), monitor);
				if (!gitCommandResult2.Succeeded)
				{
					return GitCommandResult.Failure(gitCommandResult2.Error);
				}
			}
			monitor.Update(0.0, "Stashing...");
			string text = ((!string.IsNullOrEmpty(stashMessage)) ? stashMessage.Quotify() : $"Snapshot on '{sourceString}' {DateTime.Now}");
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("stash", "create", text).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			string text2 = gitRequestResult.Stdout.Trim();
			GitRequestResult gitRequestResult2 = new GitRequest(gitModule).Command("stash", "store", text2).Execute(monitor);
			if (!gitRequestResult2.Success)
			{
				return GitCommandResult.Failure(gitRequestResult2.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
