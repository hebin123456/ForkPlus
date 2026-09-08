using System;
using System.IO;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class ResolveMergeConflictGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, ChangedFile changedFile, string content)
		{
			string path = Path.Combine(gitModule.Path, changedFile.Path);
			try
			{
				File.WriteAllText(path, content);
			}
			catch (Exception ex)
			{
				return GitCommandResult.Failure(ex);
			}
			// 问题6：写入侧四件套对齐（见 ReliableGitFlags）——File.WriteAllText 后立即 add，
			// 同秒 mtime 的 racy 窗口 + daemon 漏报时裸 add 会跳过，此处必须可靠 stat。
			GitCommand addCommand = new GitCommand(ReliableGitFlags.Prefix, "add", "--", changedFile.Path.Quotify());
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(addCommand).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
