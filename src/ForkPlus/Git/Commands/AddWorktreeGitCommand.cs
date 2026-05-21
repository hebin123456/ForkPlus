using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class AddWorktreeGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string path, string branch, JobMonitor monitor)
		{
			GitCommand command = new GitCommand(App.OverrideCredentialHelperBt, "worktree", "add", path, branch);
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).ExecuteBt(monitor);
			if (!gitRequestResult.Success)
			{
				monitor.Fail("Checkout branch as worktree failed");
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			monitor.Success("Created '" + branch + "' worktree");
			return GitCommandResult.Success();
		}

		public GitCommandResult Execute(GitModule gitModule, string path, string branch, Sha startSha, JobMonitor monitor)
		{
			GitCommand command = new GitCommand(App.OverrideCredentialHelperBt, "worktree", "add", "-b", branch, path, startSha.ToString());
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).ExecuteBt(monitor);
			if (!gitRequestResult.Success)
			{
				monitor.Fail("Create worktree failed");
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			monitor.Success("Created '" + branch + "' worktree");
			return GitCommandResult.Success();
		}
	}
}
