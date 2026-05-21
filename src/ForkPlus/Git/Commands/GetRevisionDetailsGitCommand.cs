using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class GetRevisionDetailsGitCommand
	{
		public GitCommandResult<RevisionDetails> Execute(GitModule gitModule, Sha sha, JobMonitor monitor)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("show", "--no-patch", "--pretty=format:" + RevisionDetailsParser.FormatString, sha.ToString()).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<RevisionDetails>.Failure(gitRequestResult.ToGitCommandError());
			}
			RevisionDetails revisionDetails = RevisionDetailsParser.Parse(gitRequestResult.Stdout);
			if (revisionDetails == null)
			{
				return GitCommandResult<RevisionDetails>.Failure(new GitCommandError.ParseError("Cannot parse revision details"));
			}
			return GitCommandResult<RevisionDetails>.Success(revisionDetails);
		}
	}
}
