using System;
using System.IO;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class MoveSubmoduleGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string submodulePath, string newSubmodulePath, JobMonitor monitor)
		{
			try
			{
				Directory.Delete(gitModule.MakePath(newSubmodulePath));
			}
			catch (Exception ex)
			{
				return GitCommandResult.Failure(new GitCommandError.UnknownException(ex));
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("mv", "--verbose", submodulePath.Quotify(), newSubmodulePath.Quotify()).ExecuteLong(delegate(string stdOutLine)
			{
				monitor.AppendOutputLine(stdOutLine);
			}, delegate(string line)
			{
				if (!monitor.HandleGitProgress(line))
				{
					monitor.AppendOutputLine(line);
				}
			}, monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
