using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class ResolveConflictGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, ChangedFile file, UnmergedFileVersionType version)
		{
			Log.Info($"Resolve '{file.Path}' conflict with {version} version");
			string text = file.Path.Quotify();
			if ((version == UnmergedFileVersionType.Local && file.Status == StatusType.Deleted) || (version == UnmergedFileVersionType.Remote && file.WorkingDirectoryStatus == StatusType.Deleted))
			{
				// 问题6：写入侧四件套对齐（见 ReliableGitFlags）——冲突解决的 rm/add 同 stage 家族。
				GitCommand rmCommand = new GitCommand(ReliableGitFlags.Prefix, "rm", "--", text);
				GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(rmCommand).Execute();
				if (!gitRequestResult.Success)
				{
					return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
				}
				return GitCommandResult.Success();
			}
			GitCommand checkoutIndexCommand = new GitCommand(ReliableGitFlags.Prefix, "checkout-index", "-f", $"--stage={(int)version}", "--", text);
			GitRequestResult gitRequestResult2 = new GitRequest(gitModule).Command(checkoutIndexCommand).Execute();
			if (!gitRequestResult2.Success)
			{
				return GitCommandResult.Failure(gitRequestResult2.ToGitCommandError());
			}
			GitCommand addCommand = new GitCommand(ReliableGitFlags.Prefix, "add", "--", text);
			GitRequestResult gitRequestResult3 = new GitRequest(gitModule).Command(addCommand).Execute();
			if (!gitRequestResult3.Success)
			{
				return GitCommandResult.Failure(gitRequestResult3.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}

		public GitCommandResult Execute(GitModule gitModule, SubmoduleChangedFile changedFile, Sha shaToResolve)
		{
			Log.Info($"Resolve '{changedFile.Path}' submodule conflict with {shaToResolve}");
			GitCommandResult<GitModule> gitCommandResult = new OpenGitRepositoryGitCommand().Execute(gitModule, changedFile.Submodule);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult.Failure(gitCommandResult.Error);
			}
			GitModule result = gitCommandResult.Result;
			GitCommandResult gitCommandResult2 = new CheckoutRevisionGitCommand().Execute(result, shaToResolve, new JobMonitor());
			if (!gitCommandResult2.Succeeded)
			{
				return GitCommandResult.Failure(gitCommandResult2.Error);
			}
			GitCommand submoduleAddCommand = new GitCommand(ReliableGitFlags.Prefix, "add", "--", changedFile.Path.Quotify());
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(submoduleAddCommand).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
