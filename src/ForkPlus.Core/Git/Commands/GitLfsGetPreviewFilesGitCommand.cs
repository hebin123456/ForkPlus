using System;
using System.Collections.Generic;
using System.Linq;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GitLfsGetPreviewFilesGitCommand
	{
		public GitCommandResult<string[]> Execute(GitModule gitModule, string[] patterns)
		{
			GitCommand gitCommand = new GitCommand("ls-files", "-o", "-m", "-c", "-i");
			foreach (string input in patterns)
			{
				gitCommand.Add("--exclude=" + input.Quotify());
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute(silent: true);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<string[]>.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult<string[]>.Success(new HashSet<string>(gitRequestResult.Stdout.Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries)).ToArray().ToArray());
		}
	}
}
