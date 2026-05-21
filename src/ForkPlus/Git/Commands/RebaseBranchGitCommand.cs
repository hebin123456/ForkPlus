using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class RebaseBranchGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string destination, bool rebaseMerges, bool updateRefs, JobMonitor monitor)
		{
			GitCommand gitCommand = new GitCommand(App.OverrideCredentialHelper, "-c", "core.commentChar=" + Consts.Git.CommentChar, "rebase");
			if (rebaseMerges)
			{
				gitCommand.Add("--rebase-merges");
			}
			if (updateRefs)
			{
				gitCommand.Add("--update-refs");
			}
			gitCommand.Add(destination);
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
