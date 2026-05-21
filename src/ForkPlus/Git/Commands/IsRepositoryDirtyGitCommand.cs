using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class IsRepositoryDirtyGitCommand
	{
		public GitCommandResult<bool> Execute(GitModule gitModule)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(
				"-c", "core.fsmonitor=false",
				"-c", "core.untrackedCache=false",
				"-c", "core.checkStat=default",
				"--no-optional-locks", "status", "--porcelain", "--untracked-files=no", "-z").Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<bool>.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult<bool>.Success(gitRequestResult.Stdout.Length > 0);
		}
	}
}
