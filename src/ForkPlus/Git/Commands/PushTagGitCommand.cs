using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.Git.Commands
{
	internal sealed class PushTagGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string remote, string tagFullReference, JobMonitor monitor)
		{
			GitCommand gitCommand = new GitCommand(App.OverrideCredentialHelper, "push", remote, tagFullReference);
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
			monitor.Success("Everything is up to date");
			return GitCommandResult.Success();
		}
	}
}
