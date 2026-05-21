using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class ResetCurrentBranchToRevisionGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, Sha dst, BranchResetType resetType, JobMonitor monitor)
		{
			GitCommand command = new GitCommand(App.OverrideCredentialHelperBt, "reset", GetGitParameter(resetType), dst.ToString());
			monitor.Append(null, command);
			ProcessOutputHandler processOutputHandler = new ProcessOutputHandler(monitor);
			ExecuteWithCallbackResponse executeWithCallbackResponse = new GitRequest(gitModule).Command(command).ExecuteWithCallbackBt(processOutputHandler.StdoutHandler, processOutputHandler.StderrHandler, monitor);
			if (monitor.IsCanceled)
			{
				return GitCommandResult.Failure(new GitCommandError.Cancelled());
			}
			ISpawnError error = executeWithCallbackResponse.Error;
			if (error != null)
			{
				monitor.Fail("reset failed");
				return GitCommandResult.Failure(error.ToGitCommandError());
			}
			if (!executeWithCallbackResponse.Result.Success)
			{
				monitor.Fail("reset failed");
				return GitCommandResult.Failure(new GitCommandError.GitError(processOutputHandler.FullOutput(), processOutputHandler.Stderr()));
			}
			monitor.Success("Reset to '" + dst.ToAbbreviatedString() + "'");
			return GitCommandResult.Success();
		}

		public static string GetGitParameter(BranchResetType resetType)
		{
			return resetType switch
			{
				BranchResetType.Mixed => "--mixed", 
				BranchResetType.Hard => "--hard", 
				_ => "--soft", 
			};
		}
	}
}
