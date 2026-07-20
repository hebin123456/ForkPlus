using System;
using System.IO;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetBlobGitCommand
	{
		[Null]
		public GitCommandResult<MemoryStream> Execute(GitModule gitModule, BlobTarget target)
		{
			if (target is BlobTarget.Revision revision)
			{
				ShellRequestBinaryResult shellRequestBinaryResult = new GitRequest(gitModule).Command("cat-file", "blob", (revision.Revspec + ":" + revision.File).Quotify()).ExecuteBinary();
				if (!shellRequestBinaryResult.Success)
				{
					return GitCommandResult<MemoryStream>.Failure(shellRequestBinaryResult.ToGitCommandError());
				}
				return GitCommandResult<MemoryStream>.Success(shellRequestBinaryResult.Stdout);
			}
			if (target is BlobTarget.Blob blob)
			{
				if (blob.Sha == Sha.Zero)
				{
					return GitCommandResult<MemoryStream>.Success(null);
				}
				ShellRequestBinaryResult shellRequestBinaryResult2 = new GitRequest(gitModule).Command("cat-file", "blob", $"{blob.Sha}").ExecuteBinary();
				if (!shellRequestBinaryResult2.Success)
				{
					return GitCommandResult<MemoryStream>.Failure(shellRequestBinaryResult2.ToGitCommandError());
				}
				return GitCommandResult<MemoryStream>.Success(shellRequestBinaryResult2.Stdout);
			}
			if (target is BlobTarget.Unstaged unstaged)
			{
				string path = gitModule.MakePath(unstaged.File);
				if (!File.Exists(path))
				{
					return GitCommandResult<MemoryStream>.Success(null);
				}
				try
				{
					return GitCommandResult<MemoryStream>.Success(new MemoryStream(File.ReadAllBytes(path)));
				}
				catch (Exception ex)
				{
					Log.Error("Cannot load file data:", ex);
					return GitCommandResult<MemoryStream>.Failure(ex);
				}
			}
			return GitCommandResult<MemoryStream>.Success(null);
		}
	}
}
