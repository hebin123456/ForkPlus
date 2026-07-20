using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetMergeBaseGitCommand
	{
		public GitCommandResult<Sha> Execute(GitModule gitModule, Sha one, Sha other)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("merge-base", one.ToString(), other.ToString()).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<Sha>.Failure(gitRequestResult.ToGitCommandError());
			}
			Sha? sha = Sha.Parse(gitRequestResult.Stdout.Trim());
			if (sha.HasValue)
			{
				Sha valueOrDefault = sha.GetValueOrDefault();
				return GitCommandResult<Sha>.Success(valueOrDefault);
			}
			return GitCommandResult<Sha>.Failure(new GitCommandError.ParseError("Failed to parse SHA in '" + gitRequestResult.Stdout + "'"));
		}
	}
}
