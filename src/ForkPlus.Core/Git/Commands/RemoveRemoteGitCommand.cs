using ForkPlus.Services;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	internal class RemoveRemoteGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, Remote remote, JobMonitor monitor)
		{
			GitCommand command = new GitCommand(ServiceLocator.GitEnvironment.OverrideCredentialHelper, "remote", "remove", remote.Name);
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
