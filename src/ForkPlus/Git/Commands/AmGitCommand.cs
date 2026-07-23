using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Services;

namespace ForkPlus.Git.Commands
{
	internal class AmGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string patchPath, JobMonitor monitor)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("am", "--3way", patchPath).ExecuteBt(monitor);
			if (!gitRequestResult.Success)
			{
				monitor.Fail(ServiceLocator.Localization.Current("Cannot apply patch"));
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			monitor.Success(ServiceLocator.Localization.Current("Applied"));
			return GitCommandResult.Success();
		}

		public GitCommandResult Execute(GitModule gitModule, byte[] patchData, JobMonitor monitor)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("am", "--3way").Stdin(patchData).ExecuteBt(monitor);
			if (!gitRequestResult.Success)
			{
				monitor.Fail(ServiceLocator.Localization.Current("Cannot apply patch"));
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			monitor.Success(ServiceLocator.Localization.Current("Applied"));
			return GitCommandResult.Success();
		}
	}
}
