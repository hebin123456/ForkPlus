using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class ApplyGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, bool staged, byte[] patchData)
		{
			GitCommand command = (staged ? new GitCommand("apply", "--cached", "--whitespace=nowarn", "--ignore-space-change", "-C1", "--reverse") : new GitCommand("apply", "--cached", "--whitespace=nowarn", "--ignore-space-change", "-C1"));
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).Stdin(patchData).Execute(3);
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
