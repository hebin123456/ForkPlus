using System;
using System.IO;

namespace ForkPlus.Git.Commands
{
	public class GitLfsGetCachedFileGitCommand
	{
		public GitCommandResult<MemoryStream> Execute(string gitDirectoryPath, string sha256String)
		{
			string path = Path.Combine(gitDirectoryPath, "lfs", "objects", sha256String.Substring(0, 2), sha256String.Substring(2, 2), sha256String);
			if (!File.Exists(path))
			{
				return GitCommandResult<MemoryStream>.Failure(new GitCommandError.NotFound());
			}
			try
			{
				return GitCommandResult<MemoryStream>.Success(new MemoryStream(File.ReadAllBytes(path)));
			}
			catch (Exception ex)
			{
				Log.Error("Cannot load LFS file data", ex);
				return GitCommandResult<MemoryStream>.Failure(ex);
			}
		}
	}
}
