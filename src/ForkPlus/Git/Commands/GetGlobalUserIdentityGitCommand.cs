using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	internal class GetGlobalUserIdentityGitCommand
	{
		public GitCommandResult<UserIdentity> Execute()
		{
			string name = default(GitRequest).Command("config", "--global", "user.name").Execute().Stdout?.Trim();
			string email = default(GitRequest).Command("config", "--global", "user.email").Execute().Stdout?.Trim();
			return GitCommandResult<UserIdentity>.Success(new UserIdentity(name, email));
		}
	}
}
