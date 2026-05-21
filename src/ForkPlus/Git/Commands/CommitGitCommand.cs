using System;
using System.IO;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class CommitGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string message, bool amend, bool commitAndPush, JobMonitor monitor, bool noVerify = false)
		{
			string text = gitModule.CommitMessagePath();
			try
			{
				File.WriteAllText(text, message);
			}
			catch (Exception ex)
			{
				return GitCommandResult.Failure(ex);
			}
			GitCommand gitCommand = new GitCommand("commit");
			if (amend)
			{
				gitCommand.Add("--amend");
			}
			if (gitModule.Settings.SignOff)
			{
				gitCommand.Add("--signoff");
			}
			if (noVerify)
			{
				gitCommand.Add("--no-verify");
			}
			gitCommand.Add("--file");
			gitCommand.Add(text.Quotify());
			monitor.Append(null, gitCommand);
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).ExecuteLong(delegate(string stdOutLine)
			{
				monitor.AppendOutputLine(stdOutLine);
			}, delegate(string line)
			{
				monitor.AppendOutputLine(line);
			}, monitor, 3);
			if (monitor.IsCanceled)
			{
				return GitCommandResult.Failure(new GitCommandError.Cancelled());
			}
			if (!gitRequestResult.Success)
			{
				if (GitCommandError.RepositoryIsLocked.Test(gitRequestResult.Stderr))
				{
					return GitCommandResult.Failure(new GitCommandError.RepositoryIsLocked(gitRequestResult));
				}
				return GitCommandResult.Failure(new GitCommandError.CommitFailed(message, amend, commitAndPush, monitor.Output));
			}
			return GitCommandResult.Success();
		}
	}
}
