using System;
using System.Threading;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls.Preferences;

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
			monitor.Update(monitor.TotalProgress, PreferencesLocalization.FormatCurrent("Reviewing with {0}...", aiAgent.Name));
			// 修复（2026-09-10，"AI 检视一直显示排队中"的配套）：CLI stdout 逐行到达，此前只累计
			// 不上报，非流式 CLI（如 claude -p）整个执行期间状态栏无任何变化。这里把已接收字数
			// 以"Generating... ({0} chars)"（复用 OpenAI 流式路径文案）节流上报到状态栏，
			// 每 200 字符最多一次，避免大输出时 Dispatcher.Post 洪泛。
			long receivedChars = 0L;
			long reportedChars = -1L;
			object receivedLock = new object();
			Action<string> stdoutHandler = delegate(string line)
			{
				processOutputHandler.StdoutHandler(line);
				lock (receivedLock)
				{
					receivedChars += (line?.Length ?? 0) + 1;
					if (receivedChars - reportedChars >= 200)
					{
						reportedChars = receivedChars;
						monitor.Update(monitor.TotalProgress, PreferencesLocalization.FormatCurrent("Generating... ({0} chars)", receivedChars));
					}
				}
			};
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
					.ExecuteWithCallbackBt(stdoutHandler, processOutputHandler.StderrHandler, monitor);
			}
			finally
			{
				timeoutTimer?.Dispose();
			}
			if (timedOut)
			{
				string timeoutMsg = PreferencesLocalization.Current("AI request timed out or was canceled.");
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
			monitor.Success(PreferencesLocalization.Current("Finished"));
			return GitCommandResult<string>.Success(processOutputHandler.FullOutput());
		}
	}
}
