using System;
using System.Text;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Shell.Interaction;

namespace ForkPlus.UI.CustomCommands
{
	public class RunShCustomCommandActionShellCommand
	{
		public GitCommandResult<string> Execute(ShCustomCommandAction action, CustomCommandEnvironment environment, JobMonitor monitor)
		{
			string text = environment.ReplaceVariablesWithValues(action.Script);
			text = text.Replace("\\", "\\\\").Replace("\"", "\\\"");
			monitor.AppendOutputLine("$ " + action.Path + " -c \"" + text.Replace("$", "\\$") + "\"\n");
			monitor.Update(monitor.TotalProgress, "Running...");
			StringBuilder fullStringOutput = new StringBuilder(1024);
			GitRequestResult gitRequestResult;
			try
			{
				gitRequestResult = new ShellRequest(environment.GitModule.Path, action.Path, new string[2]
				{
					"-c",
					text.Quotify()
				}).Execute(delegate(string line)
				{
					lock (fullStringOutput)
					{
						fullStringOutput.AppendLine(line);
					}
					monitor.AppendOutputLine(line);
				}, delegate(string line)
				{
					lock (fullStringOutput)
					{
						fullStringOutput.AppendLine(line);
					}
					monitor.AppendOutputLine(line);
				});
			}
			catch (Exception ex)
			{
				monitor.Fail("Failed");
				monitor.AppendOutputLine(ex.ToString());
				return GitCommandResult<string>.Failure(new GitCommandError.UnknownException(ex));
			}
			if (monitor.IsCanceled)
			{
				return GitCommandResult<string>.Failure(new GitCommandError.Cancelled());
			}
			if (gitRequestResult.Success)
			{
				monitor.Success("Finished");
				return GitCommandResult<string>.Success(fullStringOutput.ToString());
			}
			monitor.Fail("Error");
			return GitCommandResult<string>.Failure(new GitCommandError.GitError(gitRequestResult));
		}
	}
}
