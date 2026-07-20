using System;
using System.IO;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	internal sealed class CreateTagGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string tagName, string tagMessage, IGitPoint gitPoint, JobMonitor monitor)
		{
			try
			{
				using TempFileManager tempFileManager = new TempFileManager();
				string tempFilePath = tempFileManager.GetTempFilePath("fork-tagMessage.txt");
				File.WriteAllText(tempFilePath, tagMessage);
				GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("tag", "--annotate", tagName, Destination(gitPoint), "-F", tempFilePath).ExecuteBt(monitor);
				if (!gitRequestResult.Success)
				{
					return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
				}
				return GitCommandResult.Success();
			}
			catch (Exception ex)
			{
				return GitCommandResult.Failure(ex);
			}
		}

		private static string Destination(IGitPoint point)
		{
			if (point is Tag { Sha: var sha })
			{
				return sha.ToString();
			}
			return point.ObjectName;
		}
	}
}
