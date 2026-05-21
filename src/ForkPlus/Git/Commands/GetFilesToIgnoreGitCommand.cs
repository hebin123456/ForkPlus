using System;
using System.Collections.Generic;
using System.Linq;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetFilesToIgnoreGitCommand
	{
		public GitCommandResult<string[]> Execute(GitModule gitModule, string[] patterns, bool untracked = true)
		{
			GitCommand gitCommand = new GitCommand("ls-files", "--modified", "--cached", "--ignored");
			if (untracked)
			{
				gitCommand.Add("--others");
			}
			foreach (string input in patterns)
			{
				gitCommand.Add("--exclude=" + input.Quotify());
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute(silent: true);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<string[]>.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult<string[]>.Success(new HashSet<string>(gitRequestResult.Stdout.Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries)).ToArray());
		}
	}
}
