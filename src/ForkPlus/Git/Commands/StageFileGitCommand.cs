using System.Collections.Generic;
using System.Linq;
using System.Text;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Services;

namespace ForkPlus.Git.Commands
{
	public class StageFileGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, ChangedFile[] files, JobMonitor monitor)
		{
			if (files.Length < 1)
			{
				return GitCommandResult.Success();
			}
			string[] array = files.Map((ChangedFile x) => x.Path);
			byte[] bytes = Encoding.UTF8.GetBytes(string.Join("\0", array));
			HashSet<string> files2 = array.Map((string x) => PathHelper.NormalizeUnix(x)).ToHashSet();
			StageProcessOutputHandler stageProcessOutputHandler = new StageProcessOutputHandler(monitor, files2);
			ExecuteWithCallbackResponse executeWithCallbackResponse = new GitRequest(gitModule).Command("add", "--force", "--verbose", "--pathspec-from-file=-", "--pathspec-file-nul", "--").Stdin(bytes).ExecuteWithCallbackBt(stageProcessOutputHandler.StdoutHandler, stageProcessOutputHandler.StderrHandler, retryIfLocked: true, monitor);
			if (monitor.IsCanceled)
			{
				return GitCommandResult.Failure(new GitCommandError.Cancelled());
			}
			ISpawnError error = executeWithCallbackResponse.Error;
			if (error != null)
			{
				monitor.Fail(ServiceLocator.Localization.Current("stage failed"));
				return GitCommandResult.Failure(error.ToGitCommandError());
			}
			if (!executeWithCallbackResponse.Result.Success)
			{
				monitor.Fail(ServiceLocator.Localization.Current("stage failed"));
				if (GitCommandError.RepositoryIsLocked.Test(stageProcessOutputHandler.Stderr()))
				{
					return GitCommandResult.Failure(new GitCommandError.RepositoryIsLocked(stageProcessOutputHandler.FullOutput(), stageProcessOutputHandler.Stderr()));
				}
				return GitCommandResult.Failure(new GitCommandError.GitError(stageProcessOutputHandler.FullOutput(), stageProcessOutputHandler.Stderr()));
			}
			monitor.Success(ServiceLocator.Localization.Current("staged"));
			return GitCommandResult.Success();
		}
	}
}
