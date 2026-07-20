using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class InitRepositoryGitCommand
	{
		public GitCommandResult Execute(string path)
		{
			GitRequestResult gitRequestResult = default(GitRequest).CurrentDir(path).Command("init").Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
