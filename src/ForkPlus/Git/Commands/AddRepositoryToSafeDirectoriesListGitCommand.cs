using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	internal class AddRepositoryToSafeDirectoriesListGitCommand
	{
		public GitCommandResult Execute(string repositoryPath)
		{
			string text = PathHelper.NormalizeUnix(repositoryPath);
			string input = "%(prefix)/" + text;
			GitRequestResult gitRequestResult = default(GitRequest).Command("config", "--global", "--add", "safe.directory", input.Quotify()).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
