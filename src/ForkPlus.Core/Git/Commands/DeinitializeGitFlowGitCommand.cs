using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	internal class DeinitializeGitFlowGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, JobMonitor monitor)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("config", "--remove-section", "gitflow.path").ExecuteBt(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			GitRequestResult gitRequestResult2 = new GitRequest(gitModule).Command("config", "--remove-section", "gitflow.prefix").ExecuteBt(monitor);
			if (!gitRequestResult2.Success)
			{
				return GitCommandResult.Failure(gitRequestResult2.ToGitCommandError());
			}
			GitRequestResult gitRequestResult3 = new GitRequest(gitModule).Command("config", "--remove-section", "gitflow.branch").ExecuteBt(monitor);
			if (!gitRequestResult3.Success)
			{
				return GitCommandResult.Failure(gitRequestResult3.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
