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
				GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("rm", "--", text).Execute();
				if (!gitRequestResult.Success)
				{
					return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
				}
				return GitCommandResult.Success();
			}
			GitRequestResult gitRequestResult2 = new GitRequest(gitModule).Command("checkout-index", "-f", $"--stage={(int)version}", "--", text).Execute();
			if (!gitRequestResult2.Success)
			{
				return GitCommandResult.Failure(gitRequestResult2.ToGitCommandError());
			}
			GitRequestResult gitRequestResult3 = new GitRequest(gitModule).Command("add", "--", text).Execute();
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
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("add", "--", changedFile.Path.Quotify()).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
