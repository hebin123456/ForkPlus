using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class SetRepositoryLocalUserIdentityGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, UserIdentity identity)
		{
			if (identity != null)
			{
				return SaveIdentity(gitModule, identity);
			}
			return UnsetIdentity(gitModule);
		}

		private GitCommandResult SaveIdentity(GitModule gitModule, UserIdentity identity)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("config", "--local", "user.name", identity.Name.Quotify()).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			GitRequestResult gitRequestResult2 = new GitRequest(gitModule).Command("config", "--local", "user.email", identity.Email.Quotify()).Execute();
			if (!gitRequestResult2.Success)
			{
				return GitCommandResult.Failure(gitRequestResult2.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}

		private GitCommandResult UnsetIdentity(GitModule gitModule)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("config", "--local", "--unset", "user.name").Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			GitRequestResult gitRequestResult2 = new GitRequest(gitModule).Command("config", "--local", "--unset", "user.email").Execute();
			if (!gitRequestResult2.Success)
			{
				return GitCommandResult.Failure(gitRequestResult2.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
