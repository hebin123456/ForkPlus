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
			// v3.10.2：ls-files -m（modified）同样会问 fsmonitor daemon，与 status/diff 命令完全对齐四件套。
		GitCommand gitCommand = new GitCommand("-c", "core.fsmonitor=false", "-c", "core.untrackedCache=false", "-c", "core.checkStat=default", "--no-optional-locks", "ls-files", "-o", "-m", "-c", "-i");
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
