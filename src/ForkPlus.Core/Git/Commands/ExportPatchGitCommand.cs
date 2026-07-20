using System;
using System.IO;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class ExportPatchGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, Sha dst, Sha? src, string filePath)
		{
			GitCommand gitCommand = new GitCommand("format-patch", "--stdout");
			if (src.HasValue)
			{
				gitCommand.Add(src.Value.ToString() + "~.." + dst);
			}
			else
			{
				gitCommand.Add("--max-count=1");
				gitCommand.Add(dst.ToString() ?? "");
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			try
			{
				File.WriteAllText(filePath, gitRequestResult.Stdout);
				return GitCommandResult.Success();
			}
			catch (Exception ex)
			{
				Log.Error($"Cannot create patch. Error: '{ex}'");
				return GitCommandResult.Failure(new GitCommandError.UnknownException(ex));
			}
		}
	}
}
