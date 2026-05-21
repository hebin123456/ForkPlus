using System;
using System.Text;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Shell.Interaction;

namespace ForkPlus.UI.CustomCommands
{
	public class RunProcessCustomCommandActionShellCommand
	{
		public GitCommandResult<string> Execute(ProcessCustomCommandAction action, CustomCommandEnvironment environment, JobMonitor monitor)
		{
			monitor.Update(monitor.TotalProgress, "Running...");
			string stringToReplace = Environment.ExpandEnvironmentVariables(action.Path);
			stringToReplace = environment.ReplaceVariablesWithValues(stringToReplace);
			string[] array = ParseArguments(action.Parameters).CompactMap(delegate(string x)
			{
				string text = environment.ReplaceVariablesWithValues(x);
				return (!string.IsNullOrEmpty(text)) ? text : null;
			});
			monitor.AppendOutputLine("$ " + stringToReplace + " " + string.Join(" ", array) + Environment.NewLine);
			StringBuilder fullOutput = new StringBuilder();
			GitRequestResult gitRequestResult;
			try
			{
				gitRequestResult = new ShellRequest(environment.GitModule.Path, stringToReplace, array).Execute(delegate(string line)
				{
					lock (fullOutput)
					{
						fullOutput.AppendLine(line);
					}
					monitor.AppendOutputLine(line);
				}, delegate(string line)
				{
					lock (fullOutput)
					{
						fullOutput.AppendLine(line);
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
				return GitCommandResult<string>.Success(fullOutput.ToString());
			}
			monitor.Fail("Error");
			return GitCommandResult<string>.Failure(new GitCommandError.GitError(gitRequestResult));
		}

		private string[] ParseArguments(string argumentsString)
		{
			if (argumentsString == "")
			{
				return new string[0];
			}
			return argumentsString.Split(Consts.Chars.Space);
		}
	}
}
