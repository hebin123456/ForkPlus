using System;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetAllRepositoryFilesGitCommand
	{
		public GitCommandResult<string[]> Execute(GitModule gitModule)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("ls-files", " --cached", "-z").Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<string[]>.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult<string[]>.Success(gitRequestResult.Stdout.Split(Consts.Chars.Nul, StringSplitOptions.RemoveEmptyEntries));
		}
	}
}
