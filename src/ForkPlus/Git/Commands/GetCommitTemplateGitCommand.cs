using System;
using System.IO;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetCommitTemplateGitCommand
	{
		public GitCommandResult<CommitTemplate> Execute(GitModule gitModule, GitConfigLocation location = GitConfigLocation.Default)
		{
			GitCommand gitCommand = new GitCommand();
			gitCommand.Add("config");
			switch (location)
			{
			case GitConfigLocation.Global:
				gitCommand.Add("--global");
				break;
			case GitConfigLocation.Local:
				gitCommand.Add("--local");
				break;
			}
			gitCommand.Add("commit.template");
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute(silent: true);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<CommitTemplate>.Failure(gitRequestResult.ToGitCommandError());
			}
			string relativeOrAbsolutePath = gitRequestResult.Stdout.Trim();
			string absolutePath = GetAbsolutePath(gitModule, relativeOrAbsolutePath);
			if (absolutePath == null)
			{
				return GitCommandResult<CommitTemplate>.Failure(new GitCommandError.NotFound());
			}
			try
			{
				string stringValue = File.ReadAllText(absolutePath);
				return GitCommandResult<CommitTemplate>.Success(new CommitTemplate(absolutePath, stringValue));
			}
			catch (Exception ex)
			{
				Log.Error("failed to read commit template from '" + absolutePath + "'", ex);
				return GitCommandResult<CommitTemplate>.Failure(new GitCommandError.NotFound());
			}
		}

		[Null]
		private string GetAbsolutePath(GitModule gitModule, [Null] string relativeOrAbsolutePath)
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
				Log.Error("Failed to get absolute path for '" + relativeOrAbsolutePath + "'", ex);
			}
			return null;
		}
	}
}
