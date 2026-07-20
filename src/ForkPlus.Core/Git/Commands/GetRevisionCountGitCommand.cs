using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetRevisionCountGitCommand
	{
		public GitCommandResult<int> Execute(GitModule gitModule)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("rev-list", "--all", "--count").Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<int>.Failure(gitRequestResult.ToGitCommandError());
			}
			string text = gitRequestResult.Stdout.Trim();
			if (int.TryParse(text, out var result))
			{
				return GitCommandResult<int>.Success(result);
			}
			return GitCommandResult<int>.Failure(new GitCommandError.ParseError("Cannot parse '" + text + "'"));
		}
	}
}
