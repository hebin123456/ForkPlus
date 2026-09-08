using System;
using System.IO;
using System.IO.Pipes;
using ForkPlus.Git.Interaction;
using ForkPlus.IO.Ipc;

namespace ForkPlus.Git.Commands
{
	public class ContinueRebaseGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("diff-index", "HEAD", "--").Execute();
			string input = PathHelper.NormalizeUnix(Path.Combine(AppContext.BaseDirectory, Consts.ForkPlus.RIHelperFilename));
			GitCommandResult helperMissing = CheckRebaseHelperExists(input);
			if (helperMissing != null)
			{
				return helperMissing;
			}
			if (gitRequestResult.Success && gitRequestResult.Stdout == "")
			{
				GitCommand command = new GitCommand(App.OverrideCredentialHelper, "-c", "core.commentChar=" + Consts.Git.CommentChar, "-c", "sequence.editor=" + input.EscapeSpaces().Quotify(), "-c", "core.editor=" + input.EscapeSpaces().Quotify(), "rebase", "--skip");
				GitRequestResult gitRequestResult2 = new GitRequest(gitModule).Command(command).Env(RebaseInteractiveGitCommand.BuildEditorEnv(input)).Execute();
				if (!gitRequestResult2.Success)
				{
					return GitCommandResult.Failure(gitRequestResult2.ToGitCommandError());
				}
				return GitCommandResult.Success();
			}
			// rebase --continue 停在 squash/reword 之后时，git 会调用 core.editor（ForkPlus.RI.exe）
			// 编辑提交消息。此时交互式变基窗口早已关闭，RI 管道无人监听，RI.exe 连接 30 秒超时后
			// 以非零退出码结束，git 报 "there was a problem with the editor" 导致继续变基失败。
			// 临时起一个 RI 管道服务：用窗口确认时归档的消息（fork-message-archive）直接应答，
			// 让消息编辑静默通过（无归档则沿用原消息）。
			using (IpcServer riIpcServer = new IpcServer("RI", delegate (NamedPipeServerStream pipeServer)
			{
				string text = pipeServer.ReadString();
				if (text != null && text.StartsWith("prepareTodoListForRebase "))
				{
					CommitMessageArchive.TryApplyArchivedMessage(gitModule, text.Substring("prepareTodoListForRebase ".Length));
				}
				pipeServer.WriteString("start");
			}))
			{
				GitCommand command2 = new GitCommand(App.OverrideCredentialHelper, "-c", "core.commentChar=" + Consts.Git.CommentChar, "-c", "sequence.editor=" + input.EscapeSpaces().Quotify(), "-c", "core.editor=" + input.EscapeSpaces().Quotify(), "rebase", "--continue");
				GitRequestResult gitRequestResult3 = new GitRequest(gitModule).Command(command2).Env(RebaseInteractiveGitCommand.BuildEditorEnv(input)).Execute();
				if (!gitRequestResult3.Success)
				{
					return GitCommandResult.Failure(gitRequestResult3.ToGitCommandError());
				}
				return GitCommandResult.Success();
			}
		}

		internal static GitCommandResult CheckRebaseHelperExists(string helperPath)
		{
			if (File.Exists(helperPath))
			{
				return null;
			}
			return GitCommandResult.Failure(new GitCommandError.NotFound(
				"Cannot find interactive rebase helper '" + helperPath + "'. The file should be located next to the ForkPlus executable; please reinstall ForkPlus."));
		}
	}
}
