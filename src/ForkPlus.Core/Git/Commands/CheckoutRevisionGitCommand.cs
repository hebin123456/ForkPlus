using ForkPlus.Services;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class CheckoutRevisionGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, Sha sha, JobMonitor monitor, bool discard = false)
		{
			GitCommand gitCommand = new GitCommand(ServiceLocator.GitEnvironment.OverrideCredentialHelper, "checkout", "--progress");
			if (discard)
			{
				gitCommand.Add("--force");
			}
			gitCommand.Add(sha.ToString());
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
			monitor.Success(ServiceLocator.Localization.Current("Detached HEAD"));
			return GitCommandResult.Success();
		}
	}
}
