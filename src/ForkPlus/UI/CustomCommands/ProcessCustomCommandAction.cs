using System;
using System.Diagnostics;
using System.IO;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.CustomCommands
{
	public class ProcessCustomCommandAction : CustomCommandAction
	{
		public new static class Keys
		{
			public const string Type = "process";

			public const string Path = "path";

			public const string Arguments = "args";

			public const string ShowOutput = "showOutput";

			public const string WaitForExit = "waitForExit";
		}

		public string Path { get; }

		public string Parameters { get; }

		public bool ShowOutput { get; }

		public bool WaitForExit { get; }

		public ProcessCustomCommandAction(string path, string parameters, bool showOutput, bool waitForExit)
		{
			Path = path;
			Parameters = parameters;
			ShowOutput = showOutput;
			WaitForExit = waitForExit;
		}

		public override void Execute(RepositoryUserControl repositoryUserControl, string customCommandName, CustomCommandEnvironment env)
		{
			Log.Info("Run process custom action for '" + customCommandName + "'");
			string stringToReplace = Environment.ExpandEnvironmentVariables(Path);
			stringToReplace = env.ReplaceVariablesWithValues(stringToReplace);
			try
			{
				if (!File.Exists(stringToReplace))
				{
					new ErrorWindow(PreferencesLocalization.FormatCurrent("Can not find script path '{0}'", stringToReplace)).ShowDialog();
					return;
				}
			}
			catch
			{
				new ErrorWindow(PreferencesLocalization.FormatCurrent("Can not find script path '{0}'", stringToReplace)).ShowDialog();
				return;
			}
			if (!WaitForExit)
			{
				RunProcessActionNoWait(env);
				return;
			}
			string name = env.ReplaceVariablesWithValues(customCommandName);
			repositoryUserControl.JobQueue.Add(name, delegate(JobMonitor monitor)
			{
				GitCommandResult<string> customCommandResult = new RunProcessCustomCommandActionShellCommand().Execute(this, env, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (!customCommandResult.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, customCommandResult.Error).ShowDialog();
					}
					else
					{
						if (ShowOutput)
						{
							new CustomActionResultWindow(name, customCommandResult.Result).ShowDialog();
						}
						repositoryUserControl.InvalidateAndRefresh(SubDomain.All);
					}
				});
			});
		}

		private void RunProcessActionNoWait(CustomCommandEnvironment environment)
		{
			string stringToReplace = Environment.ExpandEnvironmentVariables(Path);
			stringToReplace = environment.ReplaceVariablesWithValues(stringToReplace);
			string arguments = environment.ReplaceVariablesWithValues(Parameters);
			string path = environment.GitModule.Path;
			try
			{
				using Process process = new Process();
				process.StartInfo = new ProcessStartInfo
				{
					FileName = stringToReplace,
					Arguments = arguments,
					UseShellExecute = false,
					WorkingDirectory = path,
					ErrorDialog = false
				};
				process.Start();
			}
			catch (Exception ex)
			{
				new ErrorWindow(ex.Message).ShowDialog();
			}
		}
	}
}
