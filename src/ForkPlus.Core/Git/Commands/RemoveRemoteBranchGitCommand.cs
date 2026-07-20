using ForkPlus.Services;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	internal class RemoveRemoteBranchGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, RemoteBranch remoteBranch, JobMonitor monitor)
		{
			string text = "refs/heads/" + remoteBranch.ShortName;
			GitCommand command = new GitCommand(ServiceLocator.GitEnvironment.OverrideCredentialHelper, "push", remoteBranch.Remote, "--delete", text);
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				if (BranchDoesNotExistOnServer(gitRequestResult))
				{
					return RemoveRemoteBranchLocally(gitModule, remoteBranch, monitor);
				}
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}

		public GitCommandResult RemoveRemoteBranchLocally(GitModule gitModule, RemoteBranch remoteBranch, JobMonitor monitor)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("branch", "-rd", remoteBranch.Name).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}

		private bool BranchDoesNotExistOnServer(GitRequestResult result)
		{
			if (result.Stderr.Contains("error: unable to delete"))
			{
				return result.Stderr.Contains("remote ref does not exist");
			}
			return false;
		}
	}
}
