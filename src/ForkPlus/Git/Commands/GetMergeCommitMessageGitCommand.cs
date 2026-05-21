using System;
using System.IO;

namespace ForkPlus.Git.Commands
{
	public class GetMergeCommitMessageGitCommand
	{
		public GitCommandResult<string> Execute(GitModule gitModule)
		{
			try
			{
				string path = Path.Combine(gitModule.GitDir(), "SQUASH_MSG");
				if (File.Exists(path))
				{
					string text = File.ReadAllText(path);
					if (!string.IsNullOrEmpty(text))
					{
						return GitCommandResult<string>.Success(text);
					}
				}
				string path2 = Path.Combine(gitModule.GitDir(), "MERGE_MSG");
				if (File.Exists(path2))
				{
					string text2 = File.ReadAllText(path2);
					if (!string.IsNullOrEmpty(text2))
					{
						return GitCommandResult<string>.Success(text2);
					}
				}
				string path3 = Path.Combine(gitModule.GitDir(), "rebase-apply/final-commit");
				if (File.Exists(path3))
				{
					string text3 = File.ReadAllText(path3);
					if (!string.IsNullOrEmpty(text3))
					{
						return GitCommandResult<string>.Success(text3);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to read merge commit message", ex);
			}
			return GitCommandResult<string>.Failure(new GitCommandError.NotFound());
		}
	}
}
