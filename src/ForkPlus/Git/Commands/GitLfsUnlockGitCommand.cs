using System.Collections.Generic;
using System.Text;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Services;

namespace ForkPlus.Git.Commands
{
	public class GitLfsUnlockGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, IReadOnlyList<string> filePaths, JobMonitor monitor, bool force = false)
		{
			string message = ((filePaths.Count == 1) ? ("Unlocking '" + PathHelper.GetReadableFileName(filePaths[0]) + "'") : $"Unlocking {filePaths.Count} files");
			monitor.Update(0.0, message);
			ProcessOutputHandler outputHandler = new ProcessOutputHandler(monitor);
			bool flag = false;
			List<string> list = new List<string>(4);
			GitCommand[] array = CreateUnlockGitCommands(filePaths, force);
			foreach (GitCommand command in array)
			{
				StringBuilder stderrSb = new StringBuilder();
				ExecuteWithCallbackResponse executeWithCallbackResponse = new GitRequest(gitModule).Command(command).ExecuteWithCallbackBt(delegate(string line)
				{
					outputHandler.StdoutHandler(line);
				}, delegate(string line)
				{
					outputHandler.StderrHandler(line);
					stderrSb.Append(line);
				}, monitor);
				if (monitor.IsCanceled)
				{
					return GitCommandResult.Failure(new GitCommandError.Cancelled());
				}
				ISpawnError error = executeWithCallbackResponse.Error;
				if (error != null)
				{
					monitor.Fail(ServiceLocator.Localization.Current("LFS unlock failed"));
					return GitCommandResult.Failure(error.ToGitCommandError());
				}
				if (!executeWithCallbackResponse.Result.Success)
				{
					string[] array2 = GitCommandError.LfsFileIsLocked.Match(stderrSb.ToString());
					if (array2 != null)
					{
						list.AddRange(array2);
					}
					flag = true;
				}
			}
			if (list.Count > 0)
			{
				monitor.Fail(ServiceLocator.Localization.Current("LFS unlock failed"));
				return GitCommandResult.Failure(new GitCommandError.LfsFileIsLocked(new GitRequestResult(-1, "", outputHandler.Stderr()), list));
			}
			if (flag)
			{
				monitor.Fail(ServiceLocator.Localization.Current("LFS unlock failed"));
				return GitCommandResult.Failure(new GitRequestResult(-1, "", outputHandler.Stderr()).ToGitCommandError());
			}
			string resultMessage = ((filePaths.Count == 1) ? ("Unlocked '" + PathHelper.GetReadableFileName(filePaths[0]) + "'") : $"Unlocked {filePaths.Count} files");
			monitor.Success(resultMessage);
			return GitCommandResult.Success();
		}

		private static GitCommand[] CreateUnlockGitCommands(IReadOnlyList<string> filePaths, bool force)
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
					gitCommand.Add("unlock");
					if (force)
					{
						gitCommand.Add("--force");
					}
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
