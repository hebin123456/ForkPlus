using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Services;

namespace ForkPlus.Git.Commands
{
	public class GitLfsFetchGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, Remote remote, JobMonitor monitor)
		{
			using GitLfsProgressHandler gitLfsProgressHandler = new GitLfsProgressHandler(monitor);
			GitCommand command = new GitCommand(App.OverrideCredentialHelperBt, "lfs", "fetch", remote.Name);
			monitor.Update(0.0, ServiceLocator.Localization.Current("Fetching LFS files..."));
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
			monitor.Success(ServiceLocator.Localization.Current("Everything is up to date"));
			return GitCommandResult.Success();
		}
	}
}
