using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class ContinueAmGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("am", "--continue").Execute();
			if (!gitRequestResult.Success && gitRequestResult.Stdout.Contains("No changes - did you forget to use 'git add'?"))
			{
				GitRequestResult gitRequestResult2 = new GitRequest(gitModule).Command("am", "--skip").Execute();
				if (!gitRequestResult2.Success)
				{
					return GitCommandResult.Failure(gitRequestResult2.ToGitCommandError());
				}
				return GitCommandResult.Success();
			}
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
