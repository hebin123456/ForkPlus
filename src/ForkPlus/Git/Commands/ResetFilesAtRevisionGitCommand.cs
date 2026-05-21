using System;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class ResetFilesAtRevisionGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, ChangedFile[] changedFiles, string targetString, JobMonitor monitor)
		{
			foreach (ChangedFile changedFile in changedFiles)
			{
				GitCommand command = new GitCommand(App.OverrideCredentialHelper, "reset", "-q", targetString, "--", changedFile.Path.Quotify());
				GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).Execute(monitor);
				if (!gitRequestResult.Success)
				{
					return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
				}
				GitCommand command2 = new GitCommand(App.OverrideCredentialHelper, "checkout", targetString, "--", changedFile.Path.Quotify());
				GitRequestResult gitRequestResult2 = new GitRequest(gitModule).Command(command2).Execute(monitor);
				if (!gitRequestResult2.Success)
				{
					if (!gitRequestResult2.Stderr.Contains("did not match any file(s) known to git"))
					{
						return GitCommandResult.Failure(gitRequestResult2.ToGitCommandError());
					}
					try
					{
						Fs.DeleteFile(gitModule.MakePath(changedFile.Path));
					}
					catch (Exception ex)
					{
						Log.Error("Failed to delete '" + changedFile.Path + "'", ex);
					}
				}
			}
			return GitCommandResult.Success();
		}
	}
}
