using System.Collections.Generic;
using System.Linq;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetReflogGitCommand
	{
		public GitCommandResult<string[]> Execute(GitModule gitModule)
		{
			int num = 100;
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("reflog", "HEAD", "--pretty=format:%H", $"--max-count={num}").Execute(silent: true);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<string[]>.Success(new string[0]);
			}
			return GitCommandResult<string[]>.Success(new HashSet<string>(gitRequestResult.Stdout.Split(Consts.Chars.NewLine)).ToArray());
		}
	}
}
