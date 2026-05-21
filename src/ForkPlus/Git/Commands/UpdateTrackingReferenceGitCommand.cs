using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class UpdateTrackingReferenceGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, LocalBranch localBranch, [Null] RemoteBranch trackingReference, JobMonitor monitor)
		{
			GitCommand command = ((trackingReference == null) ? new GitCommand("branch", "--unset-upstream", localBranch.Name) : new GitCommand("branch", "--set-upstream-to", trackingReference.FullReference, localBranch.Name));
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
