using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	internal sealed class RemoveLocalBranchGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string[] branches, JobMonitor monitor)
		{
			GitCommand gitCommand = new GitCommand("branch", "--delete", "--force");
			gitCommand.AddRange(branches);
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
