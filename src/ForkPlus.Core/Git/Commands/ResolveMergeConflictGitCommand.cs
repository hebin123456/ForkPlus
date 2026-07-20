using System;
using System.IO;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class ResolveMergeConflictGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, ChangedFile changedFile, string content)
		{
			string path = Path.Combine(gitModule.Path, changedFile.Path);
			try
			{
				File.WriteAllText(path, content);
			}
			catch (Exception ex)
			{
				return GitCommandResult.Failure(ex);
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("add", "--", changedFile.Path.Quotify()).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
