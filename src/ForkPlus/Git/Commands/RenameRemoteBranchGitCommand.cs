using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class RenameRemoteBranchGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, RemoteBranch remoteBranch, string newName, JobMonitor monitor)
		{
			GitCommand command = new GitCommand(App.OverrideCredentialHelper, "push", remoteBranch.Remote, remoteBranch.Sha.ToString() + ":refs/heads/" + newName, ":refs/heads/" + remoteBranch.ShortName);
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
