using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetFirstRevisionGitCommand
	{
		public GitCommandResult<Sha> Execute(GitModule gitModule, string filePath, Sha? parentSha)
		{
			GitCommand gitCommand = new GitCommand("log", "--no-show-signature", "--follow", "-n", "1", "--pretty=format:%H");
			gitCommand.Add(parentSha?.ToString() ?? "HEAD");
			gitCommand.Add("--");
			gitCommand.Add(filePath.Quotify());
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<Sha>.Failure(gitRequestResult.ToGitCommandError());
			}
			if (!Sha.TryParse(gitRequestResult.Stdout.Trim(), out var result))
			{
				return GitCommandResult<Sha>.Failure(new GitCommandError.NotFound());
			}
			return GitCommandResult<Sha>.Success(result);
		}
	}
}
