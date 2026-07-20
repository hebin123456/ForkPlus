using ForkPlus.Services;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class CherryPickGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, Sha[] shas, bool commit, bool appendOriginSha, int? parentNumber, JobMonitor monitor)
		{
			GitCommand gitCommand = new GitCommand(ServiceLocator.GitEnvironment.OverrideCredentialHelper, "cherry-pick");
			if (parentNumber.HasValue)
			{
				gitCommand.Add($"-m {parentNumber}");
			}
			if (!commit)
			{
				gitCommand.Add("--no-commit");
			}
			if (appendOriginSha)
			{
				gitCommand.Add("-x");
			}
			for (int i = 0; i < shas.Length; i++)
			{
				Sha sha = shas[i];
				gitCommand.Add(sha.ToString());
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute(monitor);
			if (GitCommandError.AutomaticMergeFailed.Match(gitRequestResult))
			{
				return GitCommandResult.Failure(new GitCommandError.AutomaticMergeFailed(gitRequestResult));
			}
			if (GitCommandError.CherryPickNothingToCommit.Match(gitRequestResult))
			{
				GitRequestResult gitRequestResult2;
				do
				{
					GitCommand command = new GitCommand(ServiceLocator.GitEnvironment.OverrideCredentialHelper, "cherry-pick", "--skip");
					gitRequestResult2 = new GitRequest(gitModule).Command(command).Execute();
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
