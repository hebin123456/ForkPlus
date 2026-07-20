using System.IO;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetBlobSizeGitCommand
	{
		[Null]
		public GitCommandResult<long?> Execute(GitModule gitModule, BlobTarget target)
		{
			if (target is BlobTarget.Revision revision)
			{
				GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("cat-file", "-s", (revision.Revspec + ":" + revision.File).Quotify()).Execute();
				if (!gitRequestResult.Success)
				{
					return GitCommandResult<long?>.Failure(gitRequestResult.ToGitCommandError());
				}
				if (long.TryParse(gitRequestResult.Stdout, out var result))
				{
					return GitCommandResult<long?>.Success(result);
				}
				return GitCommandResult<long?>.Success(null);
			}
			if (target is BlobTarget.Blob blob)
			{
				if (blob.Sha == Sha.Zero)
				{
					return GitCommandResult<long?>.Success(null);
				}
				GitRequestResult gitRequestResult2 = new GitRequest(gitModule).Command("cat-file", "-s", $"{blob.Sha}").Execute();
				if (!gitRequestResult2.Success)
				{
					return GitCommandResult<long?>.Failure(gitRequestResult2.ToGitCommandError());
				}
				if (long.TryParse(gitRequestResult2.Stdout, out var result2))
				{
					return GitCommandResult<long?>.Success(result2);
				}
				return GitCommandResult<long?>.Success(null);
			}
			if (target is BlobTarget.Unstaged unstaged)
			{
				string text = gitModule.MakePath(unstaged.File);
				if (File.Exists(text))
				{
					return GitCommandResult<long?>.Success(FileHelper.GetFileSize(text));
				}
				return GitCommandResult<long?>.Success(null);
			}
			return GitCommandResult<long?>.Success(null);
		}
	}
}
