using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class AbortMergeGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule)
		{
			GitCommand command = new GitCommand(App.OverrideCredentialHelper, "reset", "--merge");
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
