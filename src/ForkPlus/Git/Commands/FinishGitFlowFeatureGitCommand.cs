using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	internal class FinishGitFlowFeatureGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string feature, bool rebase, bool deleteBranches, bool noFastForward, JobMonitor monitor)
		{
			GitCommand gitCommand = new GitCommand(App.OverrideCredentialHelperBt, "flow", "feature", "finish");
			if (rebase)
			{
				gitCommand.Add("-r");
			}
			if (!deleteBranches)
			{
				gitCommand.Add("-k");
			}
			if (noFastForward)
			{
				gitCommand.Add("--no-ff");
			}
			gitCommand.Add(feature);
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).ExecuteBt(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
