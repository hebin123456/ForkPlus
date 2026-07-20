using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class GetFullRevisionDetailsGitCommand
	{
		private const string RevisionMessageEnd = "--ForkRevisionMessageEnd--";

		public GitCommandResult<FullRevisionDetails> Execute(GitModule gitModule, Sha sha, Submodule[] submodules, JobMonitor monitor)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("-c", "core.quotepath=false", "--no-pager", "show", "--raw", "--diff-merges=1", "--name-status", "--find-renames", "--no-color", "--no-show-signature", "--pretty=format:" + RevisionDetailsParser.FormatString + "--ForkRevisionMessageEnd--", "-z", sha.ToString()).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				GitCommandError.UnsafeRepository unsafeRepository = GitCommandError.UnsafeRepository.Test(gitRequestResult, gitModule.Path);
				if (unsafeRepository != null)
				{
					return GitCommandResult<FullRevisionDetails>.Failure(unsafeRepository);
				}
				return GitCommandResult<FullRevisionDetails>.Failure(gitRequestResult.ToGitCommandError());
			}
			string stdout = gitRequestResult.Stdout;
			int num = stdout.IndexOf("--ForkRevisionMessageEnd--");
			if (num == -1)
			{
				return GitCommandResult<FullRevisionDetails>.Failure(new GitCommandError.ParseError("Failed to parse full revision details output: '" + stdout + "'"));
			}
			RevisionDetails revisionDetails = RevisionDetailsParser.Parse(stdout.Substring(0, num));
			if (revisionDetails == null)
			{
				return GitCommandResult<FullRevisionDetails>.Failure(new GitCommandError.ParseError("Failed to parse revision details"));
			}
			ChangedFile[] changedFiles;
			if (stdout.Length > num + "--ForkRevisionMessageEnd--".Length + 1)
			{
				GitCommandResult<ChangedFile[]> gitCommandResult = GetRevisionChangedFilesGitCommand.ParseChangedFiles(stdout.Substring(num + "--ForkRevisionMessageEnd--".Length + 1), submodules);
				if (!gitCommandResult.Succeeded)
				{
					return GitCommandResult<FullRevisionDetails>.Failure(gitCommandResult.Error);
				}
				changedFiles = gitCommandResult.Result;
			}
			else
			{
				changedFiles = new ChangedFile[0];
			}
			return GitCommandResult<FullRevisionDetails>.Success(new FullRevisionDetails(revisionDetails, changedFiles));
		}
	}
}
