using ForkPlus.Services;
using System.Text.RegularExpressions;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class FastForwardMergeGitCommand
	{
		private static readonly Regex FastForwardRegEx = new Regex("^Fast-forward", RegexOptions.Multiline);

		private static readonly Regex UpToDateRegEx = new Regex("^Already up to date.", RegexOptions.Multiline);

		private static readonly Regex AheadRegEx = new Regex("^Your branch is ahead of '(.+)' (by \\d+ commit.+?)", RegexOptions.Multiline);

		public GitCommandResult Execute(GitModule gitModule, RemoteBranch remoteBranch, JobMonitor monitor)
		{
			GitCommand command = new GitCommand(ServiceLocator.GitEnvironment.OverrideCredentialHelper, "merge", "--ff-only", remoteBranch.FullReference);
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				if (GitCommandError.MergeLocalChangesWouldBeOverwritten.Match(gitRequestResult.Stderr))
				{
					return GitCommandResult.Failure(new GitCommandError.MergeLocalChangesWouldBeOverwritten(gitRequestResult));
				}
				monitor.Fail(ServiceLocator.Localization.Current("Fast-forward failed"));
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			if (UpToDateRegEx.FirstMatch(monitor.Output) != null)
			{
				monitor.Success(ServiceLocator.Localization.Current("Up to date"));
			}
			else if (FastForwardRegEx.FirstMatch(monitor.Output) != null)
			{
				monitor.Success(ServiceLocator.Localization.Current("Fast-forwarded"));
			}
			return GitCommandResult.Success();
		}
	}
}
