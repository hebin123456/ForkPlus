using System;
using System.IO;

namespace ForkPlus.Git.Commands
{
	public class ReadGitDirectoryGitCommand
	{
		public GitCommandResult<(string commonGitDir, string worktreeGitDir)> Execute(string repositoryRoot)
		{
			try
			{
				string path = PathHelper.Combine(repositoryRoot, ".git");
				if (Directory.Exists(path))
				{
					return ResolveCommonDir(PathHelper.Normalize(path));
				}
				if (File.Exists(path))
				{
					string text = TrimStart(File.ReadAllText(path).TrimEnd(), "gitdir: ");
					if (Path.IsPathRooted(text))
					{
						return ResolveCommonDir(PathHelper.Normalize(text));
					}
					return ResolveCommonDir(PathHelper.Normalize(Path.GetFullPath(PathHelper.Combine(repositoryRoot, text))));
				}
				Log.Warn("Can't find .git in " + repositoryRoot);
				return GitCommandResult<(string, string)>.Failure(new GitCommandError.NotFound());
			}
			catch (Exception ex)
			{
				Log.Error("Failed to read git dir in '" + repositoryRoot + "'", ex);
				return GitCommandResult<(string, string)>.Failure(ex);
			}
		}

		private static GitCommandResult<(string commonGitDir, string worktreeGitDir)> ResolveCommonDir(string gitDirectory)
		{
			string text = CommonDirReader.ReadCommonDir(gitDirectory);
			if (text != null)
			{
				return GitCommandResult<(string, string)>.Success((text, gitDirectory));
			}
			return GitCommandResult<(string, string)>.Success((gitDirectory, null));
		}

		private static string TrimStart(string input, string trimString)
		{
			if (input.StartsWith(trimString))
			{
				return input.Substring(trimString.Length);
			}
			return input;
		}
	}
}
