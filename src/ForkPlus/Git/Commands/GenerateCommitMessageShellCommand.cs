using System;
using System.Threading;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.Services;

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
			monitor.Update(monitor.TotalProgress, ServiceLocator.Localization.FormatCurrent("Generating with {0}...", aiAgent.Name));
			// Claude CLI 路径此前无超时，claude.exe 卡住时会无限等待。
			// 复用 OpenAI 路径的 AiReviewTimeoutSeconds 设置，超时后取消（杀死进程）。
			int timeoutSeconds = Math.Max(0, ForkPlusSettings.Default.AiReviewTimeoutSeconds);
			bool timedOut = false;
			Timer timeoutTimer = null;
			if (timeoutSeconds > 0)
			{
				timeoutTimer = new Timer(delegate
				{
					timedOut = true;
					monitor.Cancel();
				}, null, timeoutSeconds * 1000, Timeout.Infinite);
			}
			ProcessOutputHandler processOutputHandler = new ProcessOutputHandler(monitor);
			ExecuteWithCallbackResponse executeWithCallbackResponse;
			try
			{
				executeWithCallbackResponse = default(GitRequest).CurrentDir(currentDir).Path(aiAgent.Path).Command(text2)
					.ExecuteWithCallbackBt(processOutputHandler.StdoutHandler, processOutputHandler.StderrHandler, monitor);
			}
			finally
			{
				timeoutTimer?.Dispose();
			}
			if (timedOut)
			{
				string timeoutMsg = ServiceLocator.Localization.Current("AI request timed out or was canceled.");
				monitor.Fail(timeoutMsg);
				return GitCommandResult<string>.Failure(new GitCommandError.GenericError(timeoutMsg));
			}
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
			monitor.Success(ServiceLocator.Localization.Current("Finished"));
			return GitCommandResult<string>.Success(processOutputHandler.FullOutput());
		}
	}
}
