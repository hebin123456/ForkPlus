using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class RenameLocalBranchGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string branchName, string newName, JobMonitor monitor)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("branch", "--move", branchName, newName).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
