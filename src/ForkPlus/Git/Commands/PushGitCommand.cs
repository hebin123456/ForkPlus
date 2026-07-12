using System.Text.RegularExpressions;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.Git.Commands
{
	internal sealed class PushGitCommand
	{
		private static readonly Regex RejectedBranchRegEx = new Regex("^ ! \\[rejected\\]\\s+(.+) -> .+", RegexOptions.Multiline);

		private static readonly Regex NoWiFiRegEx = new Regex(" Could not resolve host: (.+)", RegexOptions.Multiline);

		private static readonly Regex UpdatedRegEx = new Regex("^updating local tracking ref 'refs/remotes/(.+)'", RegexOptions.Multiline);

		private static readonly Regex NoChangesRegEx = new Regex("^Everything up-to-date", RegexOptions.Multiline);

		public GitCommandResult Execute(GitModule gitModule, string remote, LocalBranch localBranch, [Null] RemoteBranch remoteBranch, [Null] string customRefspec, bool pushAllTags, bool force, bool track, JobMonitor monitor)
		{
			GitCommand gitCommand = new GitCommand(App.OverrideCredentialHelperBt, "-c", "push.default=upstream", "push", remote);
			if (remoteBranch != null)
			{
			 string fullReference = localBranch.FullReference;
			 string text = ((!(remoteBranch.Remote == remote)) ? ("refs/heads/" + localBranch.Name) : ("refs/heads/" + remoteBranch.ShortName));
			 gitCommand.Add(fullReference + ":" + text);
			}
			else if (customRefspec != null)
			{
			 string fullReference2 = localBranch.FullReference;
			 gitCommand.Add(fullReference2 + ":" + customRefspec);
			}
			else
			{
			 gitCommand.Add(localBranch.FullReference);
			}
			if (force)
			{
			 gitCommand.Add("--force-with-lease");
			}
			if (pushAllTags)
			{
			 gitCommand.Add("--tags");
			}
			if (track)
			{
			 gitCommand.Add("--set-upstream");
			}
			gitCommand.Add("--verbose");
			gitCommand.Add("--progress");
			monitor.Update(0.0, PreferencesLocalization.Current("Pushing..."));
			using GitLfsProgressHandler gitLfsProgressHandler = new GitLfsProgressHandler(monitor);
			ProcessOutputHandler processOutputHandler = new ProcessOutputHandler(monitor);
			ExecuteWithCallbackResponse executeWithCallbackResponse = new GitRequest(gitModule).Command(gitCommand).Env(gitLfsProgressHandler.EnvironmentVariables).ExecuteWithCallbackBt(processOutputHandler.StdoutHandler, processOutputHandler.StderrHandler, monitor);
			if (monitor.IsCanceled)
			{
				return GitCommandResult.Failure(new GitCommandError.Cancelled());
			}
			ISpawnError error = executeWithCallbackResponse.Error;
			if (error != null)
			{
				return GitCommandResult.Failure(error.ToGitCommandError());
			}
			string input = processOutputHandler.Stderr();
			if (!executeWithCallbackResponse.Result.Success)
			{
				Match match = RejectedBranchRegEx.FirstMatch(input);
				if (match != null)
				{
					string text2 = match.Groups[1].Value.TrimEnd();
					monitor.Fail(PreferencesLocalization.FormatCurrent("'{0}' rejected", text2));
				}
				else
				{
					Match match2 = NoWiFiRegEx.FirstMatch(input);
					if (match2 != null)
					{
						string text3 = match2.Groups[1].Value.TrimEnd();
						monitor.Fail(PreferencesLocalization.FormatCurrent("Could not resolve host '{0}'", text3));
					}
					else
					{
						monitor.Fail(PreferencesLocalization.Current("Push failed"));
					}
				}
				return GitCommandResult.Failure(new GitCommandError.CallbackUnknownError(processOutputHandler.FullOutput()));
			}
			if (NoChangesRegEx.FirstMatch(input) != null)
			{
				monitor.Success(PreferencesLocalization.Current("Everything is up to date"));
			}
			else
			{
				Match match3 = UpdatedRegEx.FirstMatch(input);
				if (match3 != null)
				{
					string text4 = match3.Groups[1].Value.TrimEnd();
					monitor.Success(PreferencesLocalization.FormatCurrent("Updated '{0}'", text4));
				}
			}
			return GitCommandResult.Success();
		}

		public GitCommandResult Execute(GitModule gitModule, RemoteBranch remoteBranch, string destination, JobMonitor monitor)
		{
			GitCommand gitCommand = new GitCommand(App.OverrideCredentialHelperBt, "-c", "push.default=upstream", "push", remoteBranch.Remote);
			gitCommand.Add(destination + ":refs/heads/" + remoteBranch.ShortName);
			gitCommand.Add("--force-with-lease");
			gitCommand.Add("--verbose");
			gitCommand.Add("--progress");
			monitor.Update(0.0, PreferencesLocalization.Current("Pushing..."));
			ProcessOutputHandler processOutputHandler = new ProcessOutputHandler(monitor);
			ExecuteWithCallbackResponse executeWithCallbackResponse = new GitRequest(gitModule).Command(gitCommand).ExecuteWithCallbackBt(processOutputHandler.StdoutHandler, processOutputHandler.StderrHandler, monitor);
			if (monitor.IsCanceled)
			{
				return GitCommandResult.Failure(new GitCommandError.Cancelled());
			}
			ISpawnError error = executeWithCallbackResponse.Error;
			if (error != null)
			{
				return GitCommandResult.Failure(error.ToGitCommandError());
			}
			if (!executeWithCallbackResponse.Result.Success)
			{
				monitor.Fail(processOutputHandler.Stderr());
				return GitCommandResult.Failure(new GitCommandError.GitError(processOutputHandler.FullOutput(), processOutputHandler.Stderr()));
			}
			monitor.Success(PreferencesLocalization.Current("Everything is up to date"));
			return GitCommandResult.Success();
		}
	}
}
