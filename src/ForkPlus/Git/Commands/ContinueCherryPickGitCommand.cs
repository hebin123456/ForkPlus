using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class ContinueCherryPickGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule)
		{
			GitCommand command = new GitCommand(App.OverrideCredentialHelper, "cherry-pick", "--continue");
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).Execute();
			if (GitCommandError.CherryPickNothingToCommit.Match(gitRequestResult))
			{
				GitRequestResult gitRequestResult2;
				do
				{
					GitCommand command2 = new GitCommand(App.OverrideCredentialHelper, "cherry-pick", "--skip");
					gitRequestResult2 = new GitRequest(gitModule).Command(command2).Execute();
				}
				while (GitCommandError.CherryPickNothingToCommit.Match(gitRequestResult2));
				if (!gitRequestResult2.Success)
				{
					return GitCommandResult.Failure(gitRequestResult2.ToGitCommandError());
				}
				return GitCommandResult.Success();
			}
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
