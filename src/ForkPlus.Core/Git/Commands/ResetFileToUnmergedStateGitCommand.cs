using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class ResetFileToUnmergedStateGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, ChangedFile changedFile)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("checkout", "-m", PathHelper.NormalizeUnix(changedFile.Path).Quotify()).Execute();
			if (!gitRequestResult.Success)
			{
				if (GitCommandError.RepositoryIsLocked.Test(gitRequestResult.Stderr))
				{
					return GitCommandResult.Failure(new GitCommandError.RepositoryIsLocked(gitRequestResult));
				}
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
