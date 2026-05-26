using System.Text.RegularExpressions;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.Git.Commands
{
	public class CheckoutBranchGitCommand
	{
		private static readonly Regex UpToDateRegEx = new Regex("^Your branch is up to date with '(.+)'.", RegexOptions.Multiline);

		private static readonly Regex NotUpToDateRegEx = new Regex("^Your branch is (behind)?(ahead of)? '(.+)' (by \\d+ commit.+?)", RegexOptions.Multiline);

		private static readonly Regex SwitchedToBranchRegEx = new Regex("^Switched to branch '(.+)'", RegexOptions.Multiline);

		public GitCommandResult Execute(GitModule gitModule, LocalBranch branch, JobMonitor monitor, bool discard = false)
		{
			GitCommand gitCommand = new GitCommand(App.OverrideCredentialHelper, "checkout", "--progress");
			if (discard)
			{
				gitCommand.Add("--force");
			}
			gitCommand.Add(branch.Name);
			monitor.Append(null, gitCommand);
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).ExecuteLong(delegate(string stdOutLine)
			{
				monitor.AppendOutputLine(stdOutLine);
			}, delegate(string line)
			{
				if (!monitor.HandleGitProgress(line))
				{
					monitor.AppendOutputLine(line);
				}
			}, monitor, 3);
			if (monitor.IsCanceled)
			{
				return GitCommandResult.Failure(new GitCommandError.Cancelled());
			}
			if (!gitRequestResult.Success)
			{
				monitor.Fail("Checkout failed");
				if (GitCommandError.RepositoryIsLocked.Test(gitRequestResult.Stderr))
				{
					return GitCommandResult.Failure(new GitCommandError.RepositoryIsLocked(gitRequestResult));
				}
				if (GitCommandError.CheckoutLocalChangesWouldBeOverwritten.Match(gitRequestResult.Stderr))
				{
					return GitCommandResult.Failure(new GitCommandError.CheckoutLocalChangesWouldBeOverwritten(gitRequestResult));
				}
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			Match match = UpToDateRegEx.FirstMatch(gitRequestResult.Stdout);
			if (match != null)
			{
				string text = match.Groups[1].Value.TrimEnd();
				monitor.Success(PreferencesLocalization.FormatCurrent("Up to date with '{0}'", text));
			}
			else
			{
				Match match2 = NotUpToDateRegEx.FirstMatch(gitRequestResult.Stdout);
				if (match2 != null && match2.Groups.Count == 5)
				{
					string value = match2.Groups[3].Value;
					string value2 = match2.Groups[4].Value;
					if (match2.Groups[1].Value != "")
					{
						monitor.Success(PreferencesLocalization.FormatCurrent("Behind '{0}' '{1}'", value, value2));
					}
					else if (match2.Groups[2].Value != "")
					{
						monitor.Success(PreferencesLocalization.FormatCurrent("Ahead of '{0}' '{1}'", value, value2));
					}
				}
				else
				{
					Match match3 = SwitchedToBranchRegEx.FirstMatch(gitRequestResult.Stderr);
					if (match3 != null)
					{
						string text2 = match3.Groups[1].Value.TrimEnd();
						monitor.Success(PreferencesLocalization.FormatCurrent("Switched to branch '{0}'", text2));
					}
				}
			}
			return GitCommandResult.Success();
		}
	}
}
