using ForkPlus.Services;
using System;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Shell.Interaction;

namespace ForkPlus.Git.Commands
{
	public class RunHookShellCommand
	{
		public GitCommandResult<string> Execute(GitModule gitModule, string hookName, JobMonitor monitor)
		{
			monitor.Update(monitor.TotalProgress, ServiceLocator.Localization.Current("Running..."));
			GitRequestResult gitRequestResult;
			try
			{
				string path = gitModule.HookPath(hookName);
				gitRequestResult = new ShellRequest(gitModule.Path, ServiceLocator.GitEnvironment.ShellPath, new string[2]
				{
					"--",
					PathHelper.NormalizeUnix(path).Quotify()
				}).Execute(delegate(string line)
				{
					monitor.AppendOutputLine(line);
				}, delegate(string line)
				{
					monitor.AppendOutputLine(line);
				});
			}
			catch (Exception ex)
			{
				monitor.Fail(ServiceLocator.Localization.Current("failed"));
				monitor.AppendOutputLine(ex.ToString());
				return GitCommandResult<string>.Failure(new GitCommandError.UnknownException(ex));
			}
			if (monitor.IsCanceled)
			{
				monitor.Fail(ServiceLocator.Localization.Current("Canceled"));
				return GitCommandResult<string>.Failure(new GitCommandError.Cancelled());
			}
			if (gitRequestResult.Success)
			{
				monitor.Success(ServiceLocator.Localization.Current("Finished"));
				return GitCommandResult<string>.Success(gitRequestResult.Stdout);
			}
			monitor.Fail(ServiceLocator.Localization.Current("Error"));
			return GitCommandResult<string>.Failure(new GitCommandError.GitError(gitRequestResult));
		}
	}
}
