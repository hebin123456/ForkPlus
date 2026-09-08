using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class ResetFileToUnmergedStateGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, ChangedFile changedFile)
		{
			// 问题6：写入侧四件套对齐（见 ReliableGitFlags）——checkout -m 重建 index 未合并条目。
			GitCommand checkoutCommand = new GitCommand(ReliableGitFlags.Prefix, "checkout", "-m", PathHelper.NormalizeUnix(changedFile.Path).Quotify());
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(checkoutCommand).Execute();
			if (!gitRequestResult.Success)
			{
				if (GitCommandError.RepositoryIsLocked.Test(gitRequestResult.Stderr))
				{
					return GitCommandResult.Failure(new GitCommandError.RepositoryIsLocked(gitRequestResult));
				}
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
