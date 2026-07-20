using ForkPlus.Services;
using System.Text.RegularExpressions;
using System.Threading;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class PullGitCommand
	{
		private static readonly Regex NoWiFiRegEx = new Regex(" Could not resolve host: (.+)", RegexOptions.Multiline);

		private static readonly Regex UpdatedRegEx = new Regex("^ = \\[up to date\\] +.+? +-> (.+)", RegexOptions.Multiline);

		private static readonly Regex NoChangesRegEx = new Regex("^Already up to date.", RegexOptions.Multiline);

		public GitCommandResult Execute(GitModule gitModule, string remote, [Null] RemoteBranch remoteBranch, bool rebase, bool allTags, JobMonitor monitor)
		{
			GitCommand gitCommand = new GitCommand(ServiceLocator.GitEnvironment.OverrideCredentialHelper, "pull");
			if (rebase)
			{
				gitCommand.Add("--rebase=true");
			}
			else
			{
				gitCommand.Add("--rebase=false");
			}
			gitCommand.Add(remote.Quotify());
			if (remoteBranch != null)
			{
				gitCommand.Add(remoteBranch.ShortName.Quotify());
			}
			else
			{
				gitCommand.Add("--prune");
			}
			if (allTags)
			{
				gitCommand.Add("--tags");
			}
			gitCommand.Add("--progress");
			monitor.Append(null, gitCommand);
			monitor.Update(0.0, ServiceLocator.Localization.Current("Pulling..."));
			using GitLfsProgressHandler gitLfsProgressHandler = new GitLfsProgressHandler(monitor);
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Env(gitLfsProgressHandler.EnvironmentVariables).ExecuteLong(delegate(string stdOutLine)
			{
				monitor.AppendOutputLine(stdOutLine);
			}, delegate(string stdErrLine)
			{
				if (stdErrLine.Contains("bash: /dev/tty: No such device or address"))
				{
					monitor.AppendOutputLine(ServiceLocator.Localization.Current("Cancel background fetch..."));
					Thread.Sleep(100);
					monitor.Cancel();
				}
				else if (!monitor.HandleGitProgress(stdErrLine))
				{
					monitor.AppendOutputLine(stdErrLine);
				}
			}, monitor);
			if (monitor.IsCanceled)
			{
				return GitCommandResult.Failure(new GitCommandError.Cancelled());
			}
			if (gitRequestResult.Success)
			{
				if (NoChangesRegEx.FirstMatch(monitor.Output) != null)
				{
					monitor.Success(ServiceLocator.Localization.Current("Already up to date"));
				}
				else
				{
					Match match = UpdatedRegEx.FirstMatch(monitor.Output);
					if (match != null)
					{
						string text = match.Groups[1].Value.TrimEnd();
						monitor.Success(ServiceLocator.Localization.FormatCurrent("Pulled '{0}'", text));
					}
				}
			}
			else
			{
				string stderr = gitRequestResult.Stderr;
				if (stderr != null)
				{
					Match match2 = NoWiFiRegEx.FirstMatch(stderr);
					if (match2 != null)
					{
						string text2 = match2.Groups[1].Value.TrimEnd();
						monitor.Fail(ServiceLocator.Localization.FormatCurrent("Could not resolve host '{0}'", text2));
					}
					else
					{
						monitor.Fail(stderr);
					}
				}
			}
			if (GitCommandError.TagMismatch.Match(gitRequestResult))
			{
				Remote remote2 = new Remote(remote, "", disableImplicitFetch: false, null);
				return GitCommandResult.Failure(new GitCommandError.TagMismatch(gitRequestResult, remote2));
			}
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
