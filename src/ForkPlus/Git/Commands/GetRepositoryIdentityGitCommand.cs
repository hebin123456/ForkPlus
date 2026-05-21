using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetRepositoryIdentityGitCommand
	{
		public GitCommandResult<UserIdentity> Execute(GitModule gitModule, GitConfigFileOption configLocation)
		{
			string text = ((configLocation == GitConfigFileOption.Local) ? "--local" : "--global");
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("config", text, "user.name").Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<UserIdentity>.Failure(new GitCommandError.NotFound());
			}
			GitRequestResult gitRequestResult2 = new GitRequest(gitModule).Command("config", text, "user.email").Execute();
			if (!gitRequestResult2.Success)
			{
				return GitCommandResult<UserIdentity>.Failure(new GitCommandError.NotFound());
			}
			return GitCommandResult<UserIdentity>.Success(new UserIdentity(gitRequestResult.Stdout.Trim(), gitRequestResult2.Stdout.Trim()));
		}
	}
}
