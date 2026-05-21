using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class AddRemoteGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string remoteName, string remoteUrl, JobMonitor monitor)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("remote", "add", remoteName, remoteUrl.Quotify()).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
