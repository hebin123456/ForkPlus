using System.Collections.Generic;
using System.Linq;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class SaveStashGitCommand
	{
		public GitCommandResult<bool> Execute(GitModule gitModule, string stashMessage, bool stageNewFiles, JobMonitor monitor)
		{
			if (stageNewFiles)
			{
				GitCommandResult<ChangedFilesCollection> gitCommandResult = new GetChangedFilesGitCommand().Execute(gitModule);
				if (!gitCommandResult.Succeeded)
				{
					return GitCommandResult<bool>.Failure(gitCommandResult.Error);
				}
				ChangedFile[] files = gitCommandResult.Result.ChangedFiles.Where((ChangedFile x) => x.New && !x.Staged).ToArray();
				GitCommandResult gitCommandResult2 = new StageFileGitCommand().Execute(gitModule, files, monitor);
				if (!gitCommandResult2.Succeeded)
				{
					return GitCommandResult<bool>.Failure(gitCommandResult2.Error);
				}
			}
			GitCommand gitCommand = new GitCommand("stash", "save");
			if (!string.IsNullOrEmpty(stashMessage))
			{
				gitCommand.Add("--message");
				gitCommand.Add(stashMessage.EscapeQuotes().Quotify());
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<bool>.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult<bool>.Success(!gitRequestResult.Stdout.Contains("No local changes to save"));
		}

		public GitCommandResult Execute(GitModule gitModule, string stashMessage, ChangedFile[] filesToStash, JobMonitor monitor)
		{
			List<ChangedFile> list = filesToStash.Filter((ChangedFile x) => !x.Tracked);
			if (list.Count > 0)
			{
				GitCommand gitCommand = new GitCommand("add", "-f", "--");
				foreach (ChangedFile item in list)
				{
					gitCommand.Add(item.Path.Quotify());
				}
				GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute(monitor);
				if (!gitRequestResult.Success)
				{
					if (GitCommandError.RepositoryIsLocked.Test(gitRequestResult.Stderr))
					{
						return GitCommandResult.Failure(new GitCommandError.RepositoryIsLocked(gitRequestResult));
					}
					return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
				}
			}
			List<ChangedFile> list2 = filesToStash.Filter((ChangedFile x) => x.ChangeType == ChangeType.Deleted || x.ChangeType == ChangeType.Renamed);
			if (list2.Count > 0)
			{
				GitCommand gitCommand2 = new GitCommand("reset", "HEAD", "--");
				foreach (ChangedFile item2 in list2)
				{
					if (item2.ChangeType == ChangeType.Deleted)
					{
						gitCommand2.Add(item2.Path.Quotify());
					}
					string oldPath = item2.OldPath;
					if (oldPath != null)
					{
						gitCommand2.Add(oldPath);
					}
				}
				GitRequestResult gitRequestResult2 = new GitRequest(gitModule).Command(gitCommand2).Execute(monitor);
				if (!gitRequestResult2.Success)
				{
					if (GitCommandError.RepositoryIsLocked.Test(gitRequestResult2.Stderr))
					{
						return GitCommandResult.Failure(new GitCommandError.RepositoryIsLocked(gitRequestResult2));
					}
					return GitCommandResult.Failure(gitRequestResult2.ToGitCommandError());
				}
			}
			GitCommand gitCommand3 = new GitCommand("stash", "push");
			if (!string.IsNullOrEmpty(stashMessage))
			{
				gitCommand3.Add("--message");
				gitCommand3.Add(stashMessage.EscapeQuotes().Quotify());
			}
			gitCommand3.Add("--");
			foreach (ChangedFile changedFile in filesToStash)
			{
				gitCommand3.Add(changedFile.Path.Quotify());
				string oldPath2 = changedFile.OldPath;
				if (oldPath2 != null)
				{
					gitCommand3.Add(oldPath2.Quotify());
				}
			}
			GitRequestResult gitRequestResult3 = new GitRequest(gitModule).Command(gitCommand3).Execute(monitor);
			if (!gitRequestResult3.Success)
			{
				return GitCommandResult.Failure(gitRequestResult3.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
