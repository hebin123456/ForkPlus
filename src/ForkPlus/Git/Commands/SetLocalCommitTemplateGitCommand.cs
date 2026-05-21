using System;
using System.IO;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class SetLocalCommitTemplateGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string path, string commitTemplate)
		{
			string absolutePath = GetAbsolutePath(gitModule, path);
			if (absolutePath != null)
			{
				try
				{
					File.WriteAllText(absolutePath, commitTemplate);
					return GitCommandResult.Success();
				}
				catch (Exception ex)
				{
					Log.Error("Failed to write commit template to '" + absolutePath + "'", ex);
					return GitCommandResult.Failure(new GitCommandError.UnknownException(ex));
				}
			}
			string text = Path.Combine(gitModule.CommonGitDir, "commit_msg_template.txt");
			string text2 = gitModule.MakePath(text);
			try
			{
				File.WriteAllText(text2, commitTemplate);
			}
			catch (Exception ex2)
			{
				Log.Error("Failed to write to '" + text2 + "'", ex2);
				return GitCommandResult.Failure(new GitCommandError.UnknownException(ex2));
			}
			GitCommand gitCommand = new GitCommand();
			gitCommand.Add("config");
			gitCommand.Add("--local");
			gitCommand.Add("commit.template");
			gitCommand.Add(text);
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}

		[Null]
		public static string GetAbsolutePath(GitModule gitModule, [Null] string relativeOrAbsolutePath)
		{
			if (string.IsNullOrEmpty(relativeOrAbsolutePath))
			{
				return null;
			}
			string text = gitModule.MakePath(relativeOrAbsolutePath);
			try
			{
				if (File.Exists(text))
				{
					return text;
				}
				if (File.Exists(relativeOrAbsolutePath))
				{
					return relativeOrAbsolutePath;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to check if '" + relativeOrAbsolutePath + "' exists", ex);
			}
			return null;
		}
	}
}
