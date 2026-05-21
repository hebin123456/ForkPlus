using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class AbortAmGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("am", "--abort").Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
