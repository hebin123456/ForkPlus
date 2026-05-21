using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class FastForwardGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, LocalBranch localBranch, JobMonitor monitor)
		{
			string upstreamFullReference = localBranch.UpstreamFullReference;
			if (upstreamFullReference == null)
			{
				return GitCommandResult.Failure(new GitCommandError.Bug("Fast-forward can be used only for local branches with a remote tracking reference"));
			}
			GitCommand command = ((!localBranch.IsActive) ? new GitCommand("update-ref", localBranch.FullReference, upstreamFullReference) : new GitCommand("merge", "--ff-only", upstreamFullReference));
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
