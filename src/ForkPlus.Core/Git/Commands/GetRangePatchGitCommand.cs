using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetRangePatchGitCommand
	{
		public GitCommandResult<string> Execute(GitModule gitModule, Sha src, Sha dst)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("diff", "--find-renames", "--no-ext-diff", "--no-color", "--submodule=short", "--unified=10", src.ToString() + ".." + dst).ExecuteBt();
			if (!gitRequestResult.Success)
			{
				if (gitRequestResult.Stderr.Trim() == "")
				{
					return GitCommandResult<string>.Success(gitRequestResult.Stdout);
				}
				return GitCommandResult<string>.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult<string>.Success(gitRequestResult.Stdout);
		}
	}
}
