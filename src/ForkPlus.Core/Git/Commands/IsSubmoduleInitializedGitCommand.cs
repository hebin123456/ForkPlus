using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class IsSubmoduleInitializedGitCommand
	{
		public GitCommandResult<bool> Execute(GitModule gitModule, Submodule submodule)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("submodule", "status", "--", submodule.Path).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<bool>.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult<bool>.Success(!gitRequestResult.Stdout.StartsWith("-"));
		}
	}
}
