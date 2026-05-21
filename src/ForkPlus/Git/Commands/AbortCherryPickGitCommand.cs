using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class AbortCherryPickGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule)
		{
			GitCommand command = new GitCommand(App.OverrideCredentialHelper, "cherry-pick", "--abort");
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
