using System.Text.RegularExpressions;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.Git.Commands
{
	public class CreateLocalAndTrackRemoteBranchGitCommand
	{
		private static readonly Regex SwitchedToBranchRegEx = new Regex("^Switched to a new branch '(.+)'", RegexOptions.Multiline);

		public GitCommandResult Execute(GitModule gitModule, RemoteBranch remoteBranch, string localBranchName, JobMonitor monitor, bool discard = false)
		{
			GitCommand gitCommand = new GitCommand(App.OverrideCredentialHelper, "checkout", "--track", "-b", localBranchName, remoteBranch.Name);
			if (discard)
			{
				gitCommand.Add("--force");
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				if (GitCommandError.CheckoutLocalChangesWouldBeOverwritten.Match(gitRequestResult.Stderr))
				{
					return GitCommandResult.Failure(new GitCommandError.CheckoutLocalChangesWouldBeOverwritten(gitRequestResult));
				}
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			Match match = SwitchedToBranchRegEx.FirstMatch(gitRequestResult.Stderr);
			if (match != null)
			{
				string text = match.Groups[1].Value.TrimEnd();
				monitor.Success(PreferencesLocalization.FormatCurrent("Switched to branch '{0}'", text));
			}
			return GitCommandResult.Success();
		}
	}
}
