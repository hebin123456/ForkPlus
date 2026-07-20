using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetChangedFilesCountGitCommand
	{
		public GitCommandResult<int> Execute(GitModule gitModule)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(
				"-c", "core.fsmonitor=false",
				"-c", "core.untrackedCache=false",
				"-c", "core.checkStat=default",
				"--no-optional-locks",
				"status", "--porcelain", "-uno", "-z").Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<int>.Failure(gitRequestResult.ToGitCommandError());
			}
			int num = 0;
			string stdout = gitRequestResult.Stdout;
			for (int i = 0; i < stdout.Length; i++)
			{
				if (stdout[i] == Consts.Chars.NulChar)
				{
					num++;
				}
			}
			return GitCommandResult<int>.Success(num);
		}
	}
}
