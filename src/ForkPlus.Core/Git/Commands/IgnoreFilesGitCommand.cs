using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class IgnoreFilesGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string pattern, [Null] JobMonitor monitor)
		{
			monitor = monitor ?? new JobMonitor();
			string[] patterns = pattern.Trim().Split(Consts.Chars.NewLine);
			GitCommandResult<string[]> gitCommandResult = new GetFilesToIgnoreGitCommand().Execute(gitModule, patterns, untracked: false);
			if (gitCommandResult.Succeeded && gitCommandResult.Result.Length != 0 && !new UntrackFilesGitCommand().Execute(gitModule, gitCommandResult.Result, monitor).Succeeded)
			{
				Log.Error("Cannot untrack files");
			}
			return new AddGitignorePatternGitCommand().Execute(gitModule, pattern);
		}
	}
}
