using System;
using System.IO;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	internal class FinishGitFlowHotfixGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string hotfix, bool deleteBranches, string tagMessage, JobMonitor monitor)
		{
			try
			{
				using TempFileManager tempFileManager = new TempFileManager();
				string tempFilePath = tempFileManager.GetTempFilePath("fork-tagMessage.txt");
				File.WriteAllText(tempFilePath, tagMessage);
				GitCommand gitCommand = new GitCommand(App.OverrideCredentialHelperBt, "flow", "hotfix", "finish", "-f", tempFilePath);
				if (!deleteBranches)
				{
					gitCommand.Add("-k");
				}
				gitCommand.Add(hotfix);
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
	}
}
