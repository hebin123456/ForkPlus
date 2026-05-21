using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class UpdateSubmoduleUrlGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string submodule, string newUrl, JobMonitor monitor)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("submodule", "set-url", "--", submodule, newUrl.Quotify()).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
