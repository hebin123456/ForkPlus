using ForkPlus.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Shell.Interaction
{
	public class ShellRequest
	{
		private readonly GitCommand _command;

		[Null]
		public string WorkingDirectory { get; }

		public string FilePath { get; }

		public ShellRequest([Null] string workingDirectory, string filePath, string[] arguments)
		{
			WorkingDirectory = workingDirectory;
			FilePath = filePath;
			_command = new GitCommand(arguments);
		}

		public GitRequestResult Execute()
		{
			string argumentsString = _command.ArgumentsString;
			Benchmarker benchmarker = new Benchmarker("Running '" + FilePath + " " + argumentsString + "'");
			Log.Info("Running '" + FilePath + " " + argumentsString + "'");
			Process process = new Process();
			try
			{
				process.StartInfo = CreateProcessStartInfo();
				process.Start();
				string error = string.Empty;
				Task task = Task.Run(delegate
				{
					error = process.StandardError.ReadToEnd();
				});
				string text = process.StandardOutput.ReadToEnd();
				task.Wait();
				process.WaitForExit();
				if (process.ExitCode != 0)
				{
					Log.Warn("Shell request '" + FilePath + " " + argumentsString + "' failed: '" + error + "'");
				}
				benchmarker.ReportElapsed();
				return new GitRequestResult(process.ExitCode, text.ToString(), error.ToString());
			}
			finally
			{
				if (process != null)
				{
					((IDisposable)process).Dispose();
				}
			}
		}

		public GitRequestResult Execute(Action<string> outputPipeHandler, Action<string> errorPipeHandler)
		{
			string argumentsString = _command.ArgumentsString;
			Benchmarker benchmarker = new Benchmarker("Running '" + FilePath + " " + argumentsString + "'");
			Log.Info("Running '" + FilePath + " " + argumentsString + "'");
			Process process = new Process();
			try
			{
				process.StartInfo = CreateProcessStartInfo();
				process.Start();
				StringBuilder outputSb = new StringBuilder();
				Task task = Task.Run(delegate
				{
					StreamReader standardOutput = process.StandardOutput;
					string text4 = null;
					do
					{
						text4 = standardOutput.ReadLine();
						if (text4 != null)
						{
							outputPipeHandler(text4);
							outputSb.AppendLine(text4);
						}
					}
					while (text4 != null);
				});
				StringBuilder errorSb = new StringBuilder();
				Task task2 = Task.Run(delegate
				{
					StreamReader standardError = process.StandardError;
					string text3 = null;
					do
					{
						text3 = standardError.ReadLine();
						if (text3 != null)
						{
							errorPipeHandler(text3);
							errorSb.AppendLine(text3);
						}
					}
					while (text3 != null);
				});
				task.Wait();
				task2.Wait();
				string text = outputSb.ToString();
				string text2 = errorSb.ToString();
				process.WaitForExit();
				if (process.ExitCode != 0)
				{
					Log.Warn("Shell request '" + FilePath + " " + argumentsString + "' failed: '" + text2 + "'");
				}
				benchmarker.ReportElapsed();
				return new GitRequestResult(process.ExitCode, text.ToString(), text2.ToString());
			}
			finally
			{
				if (process != null)
				{
					((IDisposable)process).Dispose();
				}
			}
		}

		private ProcessStartInfo CreateProcessStartInfo(bool redirectStdInput = false)
		{
			string text = _command.ArgumentsString;
			if (FilePath.EndsWith("git.exe"))
			{
				text = string.Join(" ", ServiceLocator.GitEnvironment.OverrideCredentialHelper) + " " + text;
			}
			ProcessStartInfo processStartInfo = new ProcessStartInfo
			{
				FileName = FilePath,
				Arguments = text,
				UseShellExecute = false,
				RedirectStandardInput = redirectStdInput,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				WorkingDirectory = WorkingDirectory,
				ErrorDialog = false,
				CreateNoWindow = true,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8
			};
			processStartInfo.EnvironmentVariables["SSH_ASKPASS_REQUIRE"] = "force";
			processStartInfo.EnvironmentVariables[Consts.Env.AskPass] = ServiceLocator.AppContext.ForkCredentialHelperPath;
			processStartInfo.EnvironmentVariables[Consts.Env.ForkPlusProcessId] = ServiceLocator.AppContext.ProcessId.ToString();
			if (WorkingDirectory != null)
			{
				processStartInfo.EnvironmentVariables["FORK_REPOSITORY_PATH"] = WorkingDirectory;
			}
			string[] sshKeys = ServiceLocator.UserSettings.SshKeys;
			if (sshKeys != null && sshKeys.Length != 0)
			{
				StringBuilder stringBuilder = new StringBuilder(1024);
				string[] array = sshKeys;
				foreach (string path in array)
				{
					stringBuilder.Append("-i '");
					stringBuilder.Append(PathHelper.NormalizeUnix(path));
					stringBuilder.Append("' ");
				}
				processStartInfo.EnvironmentVariables["GIT_SSH_COMMAND"] = "ssh " + stringBuilder.ToString() + "-F '/dev/null'";
			}
			return processStartInfo;
		}
	}
}
