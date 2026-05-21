using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class AbortRevertGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule)
		{
			GitCommand command = new GitCommand(App.OverrideCredentialHelper, "revert", "--abort");
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
