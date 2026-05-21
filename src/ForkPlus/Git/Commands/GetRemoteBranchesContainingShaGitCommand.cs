using System;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class GetRemoteBranchesContainingShaGitCommand
	{
		public GitCommandResult<string[]> Execute(GitModule gitModule, Sha sha, JobMonitor monitor)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("branch", "--list", "--remotes", "--contains", sha.ToString()).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<string[]>.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult<string[]>.Success(gitRequestResult.Stdout.Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries).Map((string x) => x.Trim()));
		}
	}
}
