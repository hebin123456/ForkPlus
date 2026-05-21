using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class DiscardFileChangesGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, ChangedFile[] files, JobMonitor monitor)
		{
			if (files.Length < 1)
			{
				return GitCommandResult.Success();
			}
			List<string> list = new List<string>();
			List<string> list2 = new List<string>();
			foreach (ChangedFile changedFile in files)
			{
				if (changedFile.New)
				{
					list.Add(changedFile.Path);
				}
				else
				{
					list2.Add(changedFile.Path);
				}
			}
			foreach (string item in list)
			{
				GitCommandResult gitCommandResult = DeleteFile(gitModule, item);
				if (!gitCommandResult.Succeeded)
				{
					return gitCommandResult;
				}
			}
			if (list2.Count > 0)
			{
				ProcessOutputHandler processOutputHandler = new ProcessOutputHandler(monitor);
				byte[] bytes = Encoding.UTF8.GetBytes(string.Join("\0", list2));
				GitCommand command = new GitCommand(App.OverrideCredentialHelperBt, "checkout-index", "--index", "--force", "--stdin", "-z");
				ExecuteWithCallbackResponse executeWithCallbackResponse = new GitRequest(gitModule).Command(command).Stdin(bytes).ExecuteWithCallbackBt(processOutputHandler.StdoutHandler, processOutputHandler.StderrHandler, retryIfLocked: true, monitor);
				if (monitor.IsCanceled)
				{
					return GitCommandResult.Failure(new GitCommandError.Cancelled());
				}
				ISpawnError error = executeWithCallbackResponse.Error;
				if (error != null)
				{
					monitor.Fail("discard failed");
					return GitCommandResult.Failure(error.ToGitCommandError());
				}
				if (!executeWithCallbackResponse.Result.Success)
				{
					monitor.Fail("discard failed");
					if (GitCommandError.RepositoryIsLocked.Test(processOutputHandler.Stderr()))
					{
						return GitCommandResult.Failure(new GitCommandError.RepositoryIsLocked(processOutputHandler.FullOutput(), processOutputHandler.Stderr()));
					}
					return GitCommandResult.Failure(new GitCommandError.GitError(processOutputHandler.FullOutput(), processOutputHandler.Stderr()));
				}
			}
			monitor.Success("discarded");
			return GitCommandResult.Success();
		}

		private GitCommandResult DeleteFile(GitModule gitModule, string filePath)
		{
			string text = gitModule.MakePath(filePath);
			try
			{
				if (Directory.Exists(text))
				{
					Directory.Delete(text, recursive: true);
				}
				else if (text == "nul" || text.EndsWith("/nul") || text.EndsWith("\\nul"))
				{
					GitRequestResult gitRequestResult = default(GitRequest).Path(App.BashPath).CurrentDir(gitModule.Path).Command("-c", "rm -f '" + text + "'")
						.ExecuteBt();
					if (!gitRequestResult.Success)
					{
						return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
					}
				}
				else
				{
					Fs.DeleteFile(text);
				}
			}
			catch (Exception ex)
			{
				if (ex is UnauthorizedAccessException && ex.Message.Contains("Access to the path") && ex.Message.Contains("is denied") && Fs.IsReadOnly(text))
				{
					try
					{
						Fs.SetAttributes(text, FileAttributes.Normal);
						Fs.DeleteFile(text);
					}
					catch (Exception arg)
					{
						Log.Error($"Cannot delete the file '{filePath}'. Message: '{arg}'");
						return GitCommandResult.Failure(ex);
					}
					return GitCommandResult.Success();
				}
				Log.Error($"Cannot delete the file '{filePath}'. Message: '{ex}'");
				return GitCommandResult.Failure(ex);
			}
			return GitCommandResult.Success();
		}
	}
}
