using System.Collections.Generic;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.Git.Commands
{
	public class GitLfsLockGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, IReadOnlyList<string> filePaths, JobMonitor monitor)
		{
			string message = ((filePaths.Count == 1) ? ("Locking '" + PathHelper.GetReadableFileName(filePaths[0]) + "'") : $"Locking {filePaths.Count} files");
			monitor.Update(0.0, message);
			ProcessOutputHandler processOutputHandler = new ProcessOutputHandler(monitor);
			GitCommandError gitCommandError = null;
			GitCommand[] array = CreateLockGitCommands(filePaths);
			foreach (GitCommand command in array)
			{
				ExecuteWithCallbackResponse executeWithCallbackResponse = new GitRequest(gitModule).Command(command).ExecuteWithCallbackBt(processOutputHandler.StdoutHandler, processOutputHandler.StderrHandler, monitor);
				if (monitor.IsCanceled)
				{
					return GitCommandResult.Failure(new GitCommandError.Cancelled());
				}
				ISpawnError error = executeWithCallbackResponse.Error;
				if (error != null)
				{
					monitor.Fail(PreferencesLocalization.Current("LFS lock failed"));
					return GitCommandResult.Failure(error.ToGitCommandError());
				}
				if (!executeWithCallbackResponse.Result.Success)
				{
					gitCommandError = new GitCommandError.GitError("", processOutputHandler.Stderr());
				}
			}
			if (gitCommandError != null)
			{
				monitor.Fail(PreferencesLocalization.Current("LFS lock failed"));
				return GitCommandResult.Failure(gitCommandError);
			}
			string resultMessage = ((filePaths.Count == 1) ? ("Locked '" + PathHelper.GetReadableFileName(filePaths[0]) + "'") : $"Locked {filePaths.Count} files");
			monitor.Success(resultMessage);
			return GitCommandResult.Success();
		}

		private static GitCommand[] CreateLockGitCommands(IReadOnlyList<string> filePaths)
		{
			List<GitCommand> list = new List<GitCommand>();
			GitCommand gitCommand = new GitCommand();
			foreach (string filePath in filePaths)
			{
				if (!gitCommand.CheckLimit(filePath))
				{
					list.Add(gitCommand);
					gitCommand = new GitCommand();
				}
				if (gitCommand.IsEmpty)
				{
					gitCommand.AddRange(App.OverrideCredentialHelperBt);
					gitCommand.Add("lfs");
					gitCommand.Add("lock");
				}
				gitCommand.Add(filePath);
			}
			if (!gitCommand.IsEmpty)
			{
				list.Add(gitCommand);
			}
			return list.ToArray();
		}
	}
}
