using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.Git.Commands
{
	internal class ApplyPatchGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string patchPath, JobMonitor monitor)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("apply", "--3way", patchPath).ExecuteBt(monitor);
			if (!gitRequestResult.Success)
			{
				monitor.Fail(PreferencesLocalization.Current("Cannot apply patch"));
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			monitor.Success(PreferencesLocalization.Current("Applied"));
			return GitCommandResult.Success();
		}

		public GitCommandResult Execute(GitModule gitModule, byte[] patchData, JobMonitor monitor)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("apply", "--3way").Stdin(patchData).ExecuteBt(monitor);
			if (!gitRequestResult.Success)
			{
				monitor.Fail(PreferencesLocalization.Current("Cannot apply patch"));
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			monitor.Success(PreferencesLocalization.Current("Applied"));
			return GitCommandResult.Success();
		}
	}
}
