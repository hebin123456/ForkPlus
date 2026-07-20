using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class AddGitLfsTrackPatternGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string[] patterns)
		{
			GitCommand gitCommand = new GitCommand("lfs", "track");
			gitCommand.AddRange(patterns);
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).ExecuteBt();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
