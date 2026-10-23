using System;
using System.IO;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	internal sealed class CreateTagGitCommand
	{
		// WS：sign=true 时加 -s（签名的附注标签）。-s 隐含 --annotate，与 --annotate 同用合法。
		public GitCommandResult Execute(GitModule gitModule, string tagName, string tagMessage, IGitPoint gitPoint, JobMonitor monitor, bool sign = false)
		{
			try
			{
				using TempFileManager tempFileManager = new TempFileManager();
				string tempFilePath = tempFileManager.GetTempFilePath("fork-tagMessage.txt");
				File.WriteAllText(tempFilePath, tagMessage);
				GitCommand gitCommand = new GitCommand("tag", "--annotate");
				if (sign)
				{
					gitCommand.Add("-s");
				}
				gitCommand.Add(tagName);
				gitCommand.Add(Destination(gitPoint));
				gitCommand.Add("-F");
				gitCommand.Add(tempFilePath);
				GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).ExecuteBt(monitor);
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
