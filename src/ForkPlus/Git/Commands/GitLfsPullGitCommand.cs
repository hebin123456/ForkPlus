using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class GitLfsPullGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, Remote remote, JobMonitor monitor)
		{
			using GitLfsProgressHandler gitLfsProgressHandler = new GitLfsProgressHandler(monitor);
			GitCommand command = new GitCommand(App.OverrideCredentialHelperBt, "lfs", "pull", remote.Name);
			monitor.Update(0.0, "Pulling LFS files...");
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).Env(gitLfsProgressHandler.EnvironmentVariables).ExecuteBt(monitor);
			if (!gitRequestResult.Success)
			{
				if (monitor.IsCanceled)
				{
					return GitCommandResult.Failure(new GitCommandError.Cancelled());
				}
				monitor.Fail(gitRequestResult.Stderr);
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			monitor.Success("Everything is up to date");
			return GitCommandResult.Success();
		}
	}
}
