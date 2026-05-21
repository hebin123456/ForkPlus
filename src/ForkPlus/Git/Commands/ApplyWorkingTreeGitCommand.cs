using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class ApplyWorkingTreeGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, byte[] discardPatchData, JobMonitor monitor)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("apply", "--whitespace=nowarn", "--ignore-whitespace").Stdin(discardPatchData).Execute(3);
			if (!gitRequestResult.Success)
			{
				if (GitCommandError.RepositoryIsLocked.Test(gitRequestResult.Stderr))
				{
					return GitCommandResult.Failure(new GitCommandError.RepositoryIsLocked(gitRequestResult));
				}
				if (GitCommandError.PatchDoesNotApply.Match(gitRequestResult.Stderr))
				{
					return GitCommandResult.Failure(new GitCommandError.PatchDoesNotApply(gitRequestResult));
				}
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
