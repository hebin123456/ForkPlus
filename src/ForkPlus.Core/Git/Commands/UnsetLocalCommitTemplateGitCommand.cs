using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class UnsetLocalCommitTemplateGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule)
		{
			GitCommand gitCommand = new GitCommand();
			gitCommand.Add("config");
			gitCommand.Add("--local");
			gitCommand.Add("--unset");
			gitCommand.Add("commit.template");
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
