using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	internal class ApplyStashGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string stashReflogName, bool deleteAfterApply, JobMonitor monitor)
		{
			string text = (stashReflogName.StartsWith("stash") ? ("refs/" + stashReflogName) : stashReflogName);
			GitCommand command = (deleteAfterApply ? new GitCommand("stash", "pop", "--index", text) : new GitCommand("stash", "apply", "--index", text));
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				if (GitCommandError.AutomaticMergeFailed.Match(gitRequestResult))
				{
					return GitCommandResult.Failure(new GitCommandError.AutomaticMergeFailed(gitRequestResult));
				}
				if (gitRequestResult.Stderr.Contains("conflicts in index. Try without --index."))
				{
					return ExecuteNoIndex(gitModule, text, deleteAfterApply, monitor);
				}
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}

		private GitCommandResult ExecuteNoIndex(GitModule gitModule, string fullStashName, bool deleteAfterApply, JobMonitor monitor)
		{
			GitCommand command = (deleteAfterApply ? new GitCommand("stash", "pop", fullStashName) : new GitCommand("stash", "apply", fullStashName));
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				if (GitCommandError.AutomaticMergeFailed.Match(gitRequestResult))
				{
					return GitCommandResult.Failure(new GitCommandError.AutomaticMergeFailed(gitRequestResult));
				}
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
