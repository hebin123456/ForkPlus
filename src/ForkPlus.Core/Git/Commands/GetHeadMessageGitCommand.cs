using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetHeadMessageGitCommand
	{
		public GitCommandResult<string> Execute(GitModule gitModule)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("log", "--no-show-signature", "-1", "--pretty=%B", "--").Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<string>.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult<string>.Success(gitRequestResult.Stdout.Trim());
		}
	}
}
