using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class RevertCommitGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, Sha sha, bool commit, int? parentNumber, JobMonitor monitor)
		{
			GitCommand gitCommand = new GitCommand(App.OverrideCredentialHelper, "revert");
			if (parentNumber.HasValue)
			{
				gitCommand.Add($"-m {parentNumber}");
			}
			if (commit)
			{
				gitCommand.Add("--no-edit");
			}
			else
			{
				gitCommand.Add("--no-commit");
			}
			gitCommand.Add(sha.ToString());
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute(monitor);
			if (GitCommandError.AutomaticMergeFailed.Match(gitRequestResult))
			{
				return GitCommandResult.Failure(new GitCommandError.AutomaticMergeFailed(gitRequestResult));
			}
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
