using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	internal class StartGitFlowFeatureGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string name, LocalBranch startPoint, JobMonitor monitor)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("flow", "feature", "start", name, startPoint.Name).ExecuteBt(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
