using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	internal class SetGlobalUserIdentityGitCommand
	{
		public GitCommandResult Execute(UserIdentity identity)
		{
			GitRequestResult gitRequestResult = default(GitRequest).Command("config", "--global", "--replace-all", "user.name", identity.Name.Quotify()).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			GitRequestResult gitRequestResult2 = default(GitRequest).Command("config", "--global", "--replace-all", "user.email", identity.Email.Quotify()).Execute();
			if (!gitRequestResult2.Success)
			{
				return GitCommandResult.Failure(gitRequestResult2.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
