using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class RenameGitmodulesSectionGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string oldName, string newName, JobMonitor monitor)
		{
			string text = ("submodule." + oldName).Quotify();
			string text2 = ("submodule." + newName).Quotify();
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("config", "--file=.gitmodules", "--rename-section", text, text2).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
