using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class DiscardAllSubmoduleChangesGitCommand
	{
		public GitCommandResult Execute(GitModule parentModule, Submodule submodule)
		{
			GitCommandResult<GitModule> gitCommandResult = new OpenGitRepositoryGitCommand().Execute(parentModule, submodule);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult.Failure(gitCommandResult.Error);
			}
			GitModule result = gitCommandResult.Result;
			GitRequestResult gitRequestResult = new GitRequest(result).Command("reset", "--hard", "HEAD").Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			GitRequestResult gitRequestResult2 = new GitRequest(result).Command("clean", "-f").Execute();
			if (!gitRequestResult2.Success)
			{
				return GitCommandResult.Failure(gitRequestResult2.ToGitCommandError());
			}
			GitRequestResult gitRequestResult3 = new GitRequest(parentModule).Command("submodule", "update", "--init", "--recursive", "--", result.Path.Quotify()).Execute();
			if (!gitRequestResult3.Success)
			{
				return GitCommandResult.Failure(gitRequestResult3.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
