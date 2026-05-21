using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class RemoveWorktreeGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string worktreePath, JobMonitor monitor)
		{
			GitCommand command = new GitCommand("worktree", "remove", "--force", worktreePath);
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).ExecuteBt(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
