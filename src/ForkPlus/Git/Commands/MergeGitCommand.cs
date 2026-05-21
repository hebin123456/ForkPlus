using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class MergeGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, Reference source, MergeType mergeType, RepositoryReferences references, JobMonitor monitor, bool mergeUnrelatedHistory = false)
		{
			GitCommand gitCommand = new GitCommand(App.OverrideCredentialHelper, "merge");
			if (references.Items.AnyItem((Reference x) => x.Name == source.Name && x.FullReference != source.FullReference))
			{
				gitCommand.Add(source.FullReference);
			}
			else
			{
				gitCommand.Add(source.Name);
			}
			switch (mergeType)
			{
			case MergeType.Squash:
				gitCommand.Add("--squash");
				break;
			case MergeType.NoFastForward:
				gitCommand.Add("--no-ff");
				break;
			case MergeType.NoCommit:
				gitCommand.Add("--no-ff");
				gitCommand.Add("--no-commit");
				break;
			}
			if (mergeUnrelatedHistory)
			{
				gitCommand.Add("--allow-unrelated-histories");
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute(monitor);
			if (GitCommandError.AutomaticMergeFailed.Match(gitRequestResult))
			{
				return GitCommandResult.Failure(new GitCommandError.AutomaticMergeFailed(gitRequestResult));
			}
			if (GitCommandError.MergeUnrelatedHistory.Match(gitRequestResult))
			{
				return GitCommandResult.Failure(new GitCommandError.MergeUnrelatedHistory(gitRequestResult, source, mergeType));
			}
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
