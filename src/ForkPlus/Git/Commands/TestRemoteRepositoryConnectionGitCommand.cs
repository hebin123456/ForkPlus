using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	internal class TestRemoteRepositoryConnectionGitCommand
	{
		public GitCommandResult Execute(string remoteUrl, JobMonitor monitor)
		{
			ProcessOutputHandler processOutputHandler = new ProcessOutputHandler(monitor, isBt: false);
			GitCommand command = new GitCommand(App.OverrideCredentialHelper, "ls-remote", remoteUrl.Quotify(), "HEAD");
			ExecuteWithCallbackResponse executeWithCallbackResponse = default(GitRequest).Command(command).ExecuteWithCallback(processOutputHandler.StdoutHandler, processOutputHandler.StderrHandler, monitor);
			ISpawnError error = executeWithCallbackResponse.Error;
			if (error != null)
			{
				return GitCommandResult.Failure(error.ToGitCommandError());
			}
			if (!executeWithCallbackResponse.Result.Success)
			{
				return GitCommandResult.Failure(new GitCommandError.CallbackUnknownError(processOutputHandler.FullOutput()));
			}
			return GitCommandResult.Success();
		}
	}
}
