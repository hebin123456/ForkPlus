using System;
using System.Threading;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.Services;

namespace ForkPlus.Git.Commands
{
	public class MakeCodeReviewShellCommand
	{
		public GitCommandResult<string> Execute(AiAgent aiAgent, AiCodeReviewTarget target, string currentDir, JobMonitor monitor)
		{
			string text;
			if (target is AiCodeReviewTarget.Branch branch)
			{
				text = "Review `" + branch.Name + "` branch by checking the following range: `" + branch.Src.ToString() + ".." + branch.Dst.ToString() + "`. Do not fetch.";
			}
			else
			{
				if (!(target is AiCodeReviewTarget.ShaRange shaRange))
				{
					return GitCommandResult<string>.Failure(new GitCommandError.GenericError("Unsupported target type"));
				}
				text = "Review commits in the following range: `" + shaRange.Src.ToString() + ".." + shaRange.Dst.ToString() + "`. Do not fetch.";
			}
			ProcessOutputHandler processOutputHandler = new ProcessOutputHandler(monitor);
			monitor.Update(monitor.TotalProgress, ServiceLocator.Localization.FormatCurrent("Reviewing with {0}...", aiAgent.Name));
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
			ExecuteWithCallbackResponse executeWithCallbackResponse;
			try
			{
				executeWithCallbackResponse = default(GitRequest).CurrentDir(currentDir).Path(aiAgent.Path).Command(text)
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
