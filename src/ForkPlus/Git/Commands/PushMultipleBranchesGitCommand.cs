using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Services;

namespace ForkPlus.Git.Commands
{
	internal sealed class PushMultipleBranchesGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string remote, LocalBranch[] localBranches, JobMonitor monitor)
		{
			GitCommand gitCommand = new GitCommand(App.OverrideCredentialHelperBt, "push", remote);
			foreach (LocalBranch localBranch in localBranches)
			{
				gitCommand.Add(localBranch.FullReference);
			}
			gitCommand.Add("--set-upstream");
			gitCommand.Add("--atomic");
			gitCommand.Add("--verbose");
			gitCommand.Add("--progress");
			monitor.Update(0.0, ServiceLocator.Localization.Current("Pushing..."));
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
			monitor.Success(ServiceLocator.Localization.Current("Everything is up to date"));
			return GitCommandResult.Success();
		}
	}
}
