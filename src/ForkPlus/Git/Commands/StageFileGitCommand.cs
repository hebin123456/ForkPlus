using System.Collections.Generic;
using System.Linq;
using System.Text;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.Git.Commands
{
	public class StageFileGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, ChangedFile[] files, JobMonitor monitor, bool bypassSafecrlf = false)
		{
			if (files.Length < 1)
			{
				return GitCommandResult.Success();
			}
			string[] array = files.Map((ChangedFile x) => x.Path);
			byte[] bytes = Encoding.UTF8.GetBytes(string.Join("\0", array));
			HashSet<string> files2 = array.Map((string x) => PathHelper.NormalizeUnix(x)).ToHashSet();
			StageProcessOutputHandler stageProcessOutputHandler = new StageProcessOutputHandler(monitor, files2);
			// 问题6：写入侧四件套对齐——裸 git add 会信 fsmonitor daemon 的"无变更"结论
			// 跳过 stat（daemon 漏报时 add 静默空操作，文件弹回未暂存），见 ReliableGitFlags。
			// v5.2.4：bypassSafecrlf=true 时在可靠前缀后追加 -c core.safecrlf=false——ErrorWindow
			// "允许转换并暂存"按钮的一次性放行重试（进程级参数，不写任何 git 配置文件）。
			string[] configParameters = (bypassSafecrlf
				? ReliableGitFlags.Prefix.Append("-c").Append("core.safecrlf=false").ToArray()
				: ReliableGitFlags.Prefix);
			GitCommand stageCommand = new GitCommand(configParameters, "add", "--force", "--verbose", "--pathspec-from-file=-", "--pathspec-file-nul", "--");
			ExecuteWithCallbackResponse executeWithCallbackResponse = new GitRequest(gitModule).Command(stageCommand).Stdin(bytes).ExecuteWithCallbackBt(stageProcessOutputHandler.StdoutHandler, stageProcessOutputHandler.StderrHandler, retryIfLocked: true, monitor);
			if (monitor.IsCanceled)
			{
				return GitCommandResult.Failure(new GitCommandError.Cancelled());
			}
			ISpawnError error = executeWithCallbackResponse.Error;
			if (error != null)
			{
				monitor.Fail(PreferencesLocalization.Current("stage failed"));
				return GitCommandResult.Failure(error.ToGitCommandError());
			}
			if (!executeWithCallbackResponse.Result.Success)
			{
				monitor.Fail(PreferencesLocalization.Current("stage failed"));
				if (GitCommandError.RepositoryIsLocked.Test(stageProcessOutputHandler.Stderr()))
				{
					return GitCommandResult.Failure(new GitCommandError.RepositoryIsLocked(stageProcessOutputHandler.FullOutput(), stageProcessOutputHandler.Stderr()));
				}
				// v5.2.4：换行符安全检查拒绝（core.safecrlf）——携带待暂存文件列表交给
				// ErrorWindow 的"允许转换并暂存"按钮做一次性放行重试。
				if (GitCommandError.LineEndingSafetyRejected.Test(stageProcessOutputHandler.Stderr()))
				{
					return GitCommandResult.Failure(new GitCommandError.LineEndingSafetyRejected(stageProcessOutputHandler.FullOutput(), stageProcessOutputHandler.Stderr(), files));
				}
				return GitCommandResult.Failure(new GitCommandError.GitError(stageProcessOutputHandler.FullOutput(), stageProcessOutputHandler.Stderr()));
			}
			monitor.Success(PreferencesLocalization.Current("staged"));
			return GitCommandResult.Success();
		}
	}
}
