using ForkPlus.Services;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	internal class RemoveMultipleRemoteBranchesGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, RemoteBranch[] remoteBranches, Remote remote, JobMonitor monitor)
		{
			string[] arguments = remoteBranches.Map((RemoteBranch x) => "refs/heads/" + x.ShortName);
			GitCommand gitCommand = new GitCommand(ServiceLocator.GitEnvironment.OverrideCredentialHelper, "push", remote.Name, "--delete");
			gitCommand.AddRange(arguments);
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
