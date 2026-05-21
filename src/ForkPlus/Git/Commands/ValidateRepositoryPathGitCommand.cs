using System.IO;

namespace ForkPlus.Git.Commands
{
	public class ValidateRepositoryPathGitCommand
	{
		public RepositoryValidState Execute(string path)
		{
			path = PathHelper.Normalize(path);
			if (!Directory.Exists(path))
			{
				return RepositoryValidState.Invalid;
			}
			GitCommandResult<(string, string)> gitCommandResult = new ReadGitDirectoryGitCommand().Execute(path);
			if (!gitCommandResult.Succeeded)
			{
				return RepositoryValidState.Invalid;
			}
			(string, string) result = gitCommandResult.Result;
			string item = result.Item1;
			string text = result.Item2;
			bool flag = text != null;
			if (text == null)
			{
				text = item;
			}
			bool flag2 = text.Contains("\\.git\\modules\\");
			string path2 = PathHelper.Combine(text, "HEAD");
			string path3 = PathHelper.Combine(item, "refs");
			if (!File.Exists(path2) || !Directory.Exists(path3))
			{
				return RepositoryValidState.Invalid;
			}
			string path4 = PathHelper.Combine(item, "objects");
			if (!flag && !Directory.Exists(path4))
			{
				return RepositoryValidState.Invalid;
			}
			if (flag)
			{
				return RepositoryValidState.ValidWorktree;
			}
			if (flag2)
			{
				return RepositoryValidState.ValidSubmodule;
			}
			return RepositoryValidState.ValidRepository;
		}
	}
}
