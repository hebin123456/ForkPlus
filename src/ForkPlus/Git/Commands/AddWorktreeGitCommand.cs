using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.UI.UserControls.Preferences;

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
				monitor.Fail(PreferencesLocalization.Current("Checkout branch as worktree failed"));
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			monitor.Success(PreferencesLocalization.FormatCurrent("Created '{0}' worktree", branch));
			return GitCommandResult.Success();
		}

		public GitCommandResult Execute(GitModule gitModule, string path, string branch, Sha startSha, JobMonitor monitor)
		{
			GitCommand command = new GitCommand(App.OverrideCredentialHelperBt, "worktree", "add", "-b", branch, path, startSha.ToString());
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).ExecuteBt(monitor);
			if (!gitRequestResult.Success)
			{
				monitor.Fail(PreferencesLocalization.Current("Create worktree failed"));
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			monitor.Success(PreferencesLocalization.FormatCurrent("Created '{0}' worktree", branch));
			return GitCommandResult.Success();
		}
	}
}
