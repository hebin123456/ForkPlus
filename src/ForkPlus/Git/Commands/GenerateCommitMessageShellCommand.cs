using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Settings;

namespace ForkPlus.Git.Commands
{
	public class GenerateCommitMessageShellCommand
	{
		public GitCommandResult<string> Execute(AiAgent aiAgent, string currentDir, bool amend, JobMonitor monitor)
		{
			int pageGuideLinePosition = ForkPlusSettings.Default.PageGuideLinePosition;
			int commitSubjectLowLimit = ForkPlusSettings.Default.CommitSubjectLowLimit;
			int commitSubjectHighLimit = ForkPlusSettings.Default.CommitSubjectHighLimit;
			string text = (amend ? "staged changes combined with the last commit (i.e. `git diff --cached HEAD^`)" : "staged changes");
			string text2 = $"Generate commit message for {text}.\n- Only respond with the commit message.\n- Start directly with the subject line (no preamble).\n- The header must be less than {commitSubjectLowLimit} (soft limit) and {commitSubjectHighLimit} (hard limit).\n- Hard wrap lines at {pageGuideLinePosition} characters.\n- Don't use the 'generated' footer.".Replace("\r", "");
			monitor.Update(monitor.TotalProgress, "Generating with " + aiAgent.Name + "...");
			ProcessOutputHandler processOutputHandler = new ProcessOutputHandler(monitor);
			ExecuteWithCallbackResponse executeWithCallbackResponse = default(GitRequest).CurrentDir(currentDir).Path(aiAgent.Path).Command(text2)
				.ExecuteWithCallbackBt(processOutputHandler.StdoutHandler, processOutputHandler.StderrHandler, monitor);
			if (monitor.IsCanceled)
			{
				return GitCommandResult<string>.Failure(new GitCommandError.Cancelled());
			}
			ISpawnError error = executeWithCallbackResponse.Error;
			if (error != null)
			{
				return GitCommandResult<string>.Failure(error.ToGitCommandError());
			}
			if (!executeWithCallbackResponse.Result.Success)
			{
				monitor.Fail(processOutputHandler.Stderr());
				return GitCommandResult<string>.Failure(new GitCommandError.GitError(processOutputHandler.FullOutput(), processOutputHandler.Stderr()));
			}
			monitor.Success("Finished");
			return GitCommandResult<string>.Success(processOutputHandler.FullOutput());
		}
	}
}
