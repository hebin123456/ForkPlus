using System.Threading;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class CloneGitCommand
	{
		public GitCommandResult Execute(string url, bool recurseSubmodules, string destinationDirectory, JobMonitor monitor)
		{
			GitCommand gitCommand = new GitCommand(App.OverrideCredentialHelper, "clone");
			if (recurseSubmodules)
			{
				gitCommand.Add("--recurse-submodules");
			}
			gitCommand.AddRange(url.Quotify(), destinationDirectory.Quotify(), "--progress", "--verbose");
			monitor.Append(null, gitCommand);
			monitor.Update(0.0, "Cloning...");
			GitRequestResult gitRequestResult = default(GitRequest).Command(gitCommand).ExecuteLong(delegate(string stdOutLine)
			{
				monitor.AppendOutputLine(stdOutLine);
			}, delegate(string stdErrLine)
			{
				if (stdErrLine.Contains("bash: /dev/tty: No such device or address"))
				{
					monitor.AppendOutputLine("Cancel...");
					Thread.Sleep(100);
					monitor.Cancel();
				}
				else
				{
					Log.Debug(stdErrLine);
					if (!monitor.HandleGitProgress(stdErrLine))
					{
						monitor.AppendOutputLine(stdErrLine);
					}
				}
			}, monitor);
			if (monitor.IsCanceled)
			{
				return GitCommandResult.Failure(new GitCommandError.Cancelled());
			}
			if (gitRequestResult.Success)
			{
				monitor.Success("Cloned");
				GitCommandResult<GitModule> gitCommandResult = new OpenGitRepositoryGitCommand().Execute(destinationDirectory);
				if (!gitCommandResult.Succeeded)
				{
					return gitCommandResult.ToGitCommandResult();
				}
				GitCommandResult<RepositoryState> gitCommandResult2 = new GetRepositoryStateGitCommand().Execute(gitCommandResult.Result, new ChangedFile[0]);
				if (!gitCommandResult2.Succeeded)
				{
					return gitCommandResult2.ToGitCommandResult();
				}
				if (gitCommandResult2.Result is RepositoryState.RebaseInProgress)
				{
					string text = "error: Cloning into '" + destinationDirectory + "' failed";
					monitor.Fail(text);
					return GitCommandResult.Failure(new GitCommandError.GitError(text));
				}
				return GitCommandResult.Success();
			}
			monitor.Fail(gitRequestResult.Stderr);
			return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
		}
	}
}
