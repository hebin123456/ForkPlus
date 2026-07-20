using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class DeleteSubmoduleGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string submodulePath, JobMonitor monitor)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("submodule", "deinit", "-f", submodulePath.Quotify()).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			GitRequestResult gitRequestResult2 = new GitRequest(gitModule).Command("rm", "-r", submodulePath.Quotify()).Execute(monitor);
			if (!gitRequestResult2.Success)
			{
				return GitCommandResult.Failure(gitRequestResult2.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
