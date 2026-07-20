using ForkPlus.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ForkPlus.Biturbo;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Interaction
{
	public struct GitRequest
	{
		[Null]
		private readonly string _path;

		[Null]
		private readonly string _currentDir;

		[Null]
		private readonly GitCommand _command;

		[Null]
		private (string, string)[] _env;

		[Null]
		private readonly byte[] _stdin;

		public GitRequest(GitModule gitModule)
			: this(null, gitModule.Path, null, null, null)
		{
		}

		public GitRequest([Null] string path, [Null] string currentDir, [Null] GitCommand command, [Null] (string, string)[] env, [Null] byte[] stdin)
		{
			_path = path;
			_currentDir = currentDir;
			_command = command;
			_env = env;
			_stdin = stdin;
		}

		public GitRequest Command([Null] GitCommand command)
		{
			return new GitRequest(_path, _currentDir, command, _env, _stdin);
		}

		public GitRequest Command(params string[] args)
		{
			return new GitRequest(_path, _currentDir, new GitCommand(args), _env, _stdin);
		}

		public GitRequest CurrentDir([Null] string currentDir)
		{
			return new GitRequest(_path, currentDir, _command, _env, _stdin);
		}

		public GitRequest Path([Null] string path)
		{
			return new GitRequest(path, _currentDir, _command, _env, _stdin);
		}

		public GitRequest Env([Null] (string, string)[] env)
		{
			return new GitRequest(_path, _currentDir, _command, env, _stdin);
		}

		public GitRequest Stdin(byte[] stdin = null)
		{
			return new GitRequest(_path, _currentDir, _command, _env, stdin);
		}

		public GitRequestResult ExecuteBt([Null] JobMonitor monitor = null, bool silent = false)
		{
			Benchmarker benchmarker = new Benchmarker("bt " + _command?.ArgumentsString);
			try
			{
				monitor?.Append(_path, _command);
				string path = _path ?? ServiceLocator.GitEnvironment.GitPath;
				string currentDir = _currentDir;
				string[] args = _command?.ToArray() ?? new string[0];
				string[] env = CreateDefaultEnv(currentDir, _env);
				Result<(int, string, string), GitCommandError> result = ChildProcess.Execute(path, currentDir, args, env, _stdin, DecodeString, DecodeString);
				if (!result.IsOk)
				{
					return new GitRequestResult(-1, "", result.Error.FriendlyDescription);
				}
				int item = result.Value.Item1;
				string item2 = result.Value.Item2;
				string item3 = result.Value.Item3;
				monitor?.AppendOutputLine(item2);
				monitor?.AppendOutputLine(item3);
				if (item != 0 && !silent)
				{
					Log.Warn("Git request failed '" + _command?.ArgumentsString + "':\n" + item3);
				}
				return new GitRequestResult(item, item2, item3);
			}
			finally
			{
				((IDisposable)benchmarker).Dispose();
			}
		}

		public ExecuteWithCallbackResponse ExecuteWithCallbackBt(Action<string> stdoutPipeHandler, Action<string> stderrPipeHandler, bool retryIfLocked, JobMonitor monitor)
		{
			int num = 0;
			ExecuteWithCallbackResponse result;
			while (true)
			{
				num++;
				if (num > 1)
				{
					monitor.AppendOutputLine("\nRepository is locked. Retrying...\n");
					Thread.Sleep(num * 500);
				}
				bool isLocked = false;
				Action<string> stderrPipeHandler2 = (retryIfLocked ? ((Action<string>)delegate(string l)
				{
					if (GitCommandError.RepositoryIsLocked.Test(l))
					{
						isLocked = true;
					}
					stderrPipeHandler(l);
				}) : stderrPipeHandler);
				result = ExecuteWithCallbackBt(stdoutPipeHandler, stderrPipeHandler2, monitor);
				if (result.Result.Success)
				{
					return result;
				}
				if (!isLocked || num >= 3)
				{
					break;
				}
			}
			return result;
		}

		public ExecuteWithCallbackResponse ExecuteWithCallbackBt(Action<string> stdoutPipeHandler, Action<string> stderrPipeHandler, JobMonitor monitor)
		{
			monitor.Append(_path, _command);
			_ = _command;
			Benchmarker benchmarker = new Benchmarker("bt " + _command?.ArgumentsString);
			try
			{
				using (new GCHandleProvider(this))
				{
					string path = _path ?? ServiceLocator.GitEnvironment.GitPath;
					string currentDir = _currentDir;
					string[] args = _command?.ToArray() ?? new string[0];
					string[] env = CreateDefaultEnv(currentDir, _env);
					Result<int, ISpawnError> result = ChildProcess.SpawnWithCallback(path, currentDir, args, env, _stdin, stdoutPipeHandler, stderrPipeHandler, monitor);
					if (!result.IsOk)
					{
						return ExecuteWithCallbackResponse.Failure(result.Error);
					}
					int value = result.Value;
					if (value != 0)
					{
						Log.Warn("Git request failed '" + _command?.ArgumentsString + "'");
					}
					return ExecuteWithCallbackResponse.Create(value);
				}
			}
			finally
			{
				((IDisposable)benchmarker).Dispose();
			}
		}

		private static string DecodeString(byte[] bytes)
		{
			try
			{
				return Encoding.UTF8.GetString(bytes);
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to decode output as utf8", ex);
			}
			return "";
		}

		public GitRequestResult Execute(bool silent = false)
		{
			if (!File.Exists(ServiceLocator.GitEnvironment.GitPath))
			{
				return new GitRequestResult(-1, string.Empty, "Cannot find git instance at: '" + ServiceLocator.GitEnvironment.GitPath + "'");
			}
			using (new Benchmarker(_command?.ArgumentsString ?? ""))
			{
				Process process = new Process();
				try
				{
					process.StartInfo = CreateGitProcessStartInfo(_currentDir);
					try
					{
						process.Start();
						string error = string.Empty;
						Task task = Task.Run(delegate
						{
							error = process.StandardError.ReadToEnd();
						});
						if (_stdin != null && _stdin.Length != 0)
						{
							process.StandardInput.BaseStream.Write(_stdin, 0, _stdin.Length);
							process.StandardInput.Close();
						}
						string text = process.StandardOutput.ReadToEnd();
						task.Wait();
						if (process.ExitCode != 0 && !silent)
						{
							Log.Warn("Git request failed '" + _command?.ArgumentsString + "':\n" + error);
						}
						return new GitRequestResult(process.ExitCode, text.ToString(), error.ToString());
					}
					catch (Exception ex)
					{
						Log.Error(ServiceLocator.GitEnvironment.GitPath, ex);
						return new GitRequestResult(-1, string.Empty, ex.ToString());
					}
				}
				finally
				{
					if (process != null)
					{
						((IDisposable)process).Dispose();
					}
				}
			}
		}

		public GitRequestResult Execute(JobMonitor monitor, bool silent = false, bool appendOutput = true)
		{
			if (!File.Exists(ServiceLocator.GitEnvironment.GitPath))
			{
				return new GitRequestResult(-1, string.Empty, "Cannot find git instance at: '" + ServiceLocator.GitEnvironment.GitPath + "'");
			}
			if (appendOutput)
			{
				monitor.Append(_path, _command);
			}
			using (new Benchmarker(_command?.ArgumentsString ?? ""))
			{
				Process process = new Process();
				try
				{
					process.StartInfo = CreateGitProcessStartInfo(_currentDir, _env);
					monitor?.SetCancellationAction(delegate
					{
						process.SendSigintSignal();
					});
					try
					{
						process.Start();
						string error = string.Empty;
						Task task = Task.Run(delegate
						{
							error = process.StandardError.ReadToEnd();
						});
						string text = process.StandardOutput.ReadToEnd();
						task.Wait();
						if (appendOutput)
						{
							monitor.AppendOutputLine(text);
							monitor.AppendOutputLine(error);
						}
						if (process.ExitCode != 0 && !silent)
						{
							Log.Warn("Git request failed '" + _command?.ArgumentsString + "':\n" + error);
						}
						return new GitRequestResult(process.ExitCode, text.ToString(), error.ToString());
					}
					catch (Exception ex)
					{
						Log.Error(ServiceLocator.GitEnvironment.GitPath, ex);
						return new GitRequestResult(-1, string.Empty, ex.ToString());
					}
					finally
					{
						monitor?.SetCancellationAction(null);
					}
				}
				finally
				{
					if (process != null)
					{
						((IDisposable)process).Dispose();
					}
				}
			}
		}

		public GitRequestResult ExecuteLong(Action<string> outputPipeHandler, Action<string> errorPipeHandler, JobMonitor monitor)
		{
			if (!File.Exists(ServiceLocator.GitEnvironment.GitPath))
			{
				return new GitRequestResult(-1, string.Empty, "Cannot find git instance at: '" + ServiceLocator.GitEnvironment.GitPath + "'");
			}
			using (new Benchmarker(_command?.ArgumentsString ?? ""))
			{
				Process process = new Process();
				try
				{
					process.StartInfo = CreateGitProcessStartInfo(_currentDir, _env);
					monitor?.SetCancellationAction(delegate
					{
						process.SendSigintSignal();
					});
					try
					{
						process.Start();
						StringBuilder outputSb = new StringBuilder();
						Task task = Task.Run(delegate
						{
							StreamReader standardOutput = process.StandardOutput;
							string text3 = null;
							do
							{
								text3 = standardOutput.ReadLine();
								if (text3 != null)
								{
									outputPipeHandler(text3);
									outputSb.AppendLine(text3);
								}
							}
							while (text3 != null);
						});
						StringBuilder errorSb = new StringBuilder();
						Task task2 = new Task(delegate
						{
							StreamReader standardError = process.StandardError;
							string text2 = null;
							do
							{
								text2 = standardError.ReadLine();
								if (text2 != null)
								{
									errorPipeHandler(text2);
									errorSb.AppendLine(text2);
								}
							}
							while (text2 != null);
						}, TaskCreationOptions.LongRunning);
						task2.Start();
						task.Wait();
						task2.Wait();
						string stdout = outputSb.ToString();
						string text = errorSb.ToString();
						if (process.ExitCode != 0)
						{
							Log.Warn("Git request failed '" + _command?.ArgumentsString + "':\n" + text);
						}
						return new GitRequestResult(process.ExitCode, stdout, text);
					}
					catch (Exception ex)
					{
						Log.Error(ServiceLocator.GitEnvironment.GitPath, ex);
						return new GitRequestResult(-1, string.Empty, ex.ToString());
					}
					finally
					{
						monitor?.SetCancellationAction(null);
					}
				}
				finally
				{
					if (process != null)
					{
						((IDisposable)process).Dispose();
					}
				}
			}
		}

		public ExecuteWithCallbackResponse ExecuteWithCallback(Action<string> stdoutPipeHandler, Action<string> stderrPipeHandler, JobMonitor monitor)
		{
			monitor.Append(_path, _command);
			if (!File.Exists(ServiceLocator.GitEnvironment.GitPath))
			{
				return ExecuteWithCallbackResponse.Failure(new GenericError("Cannot find git instance at: '" + ServiceLocator.GitEnvironment.GitPath + "'"));
			}
			using (new Benchmarker(_command?.ArgumentsString ?? ""))
			{
				Process process = new Process();
				try
				{
					process.StartInfo = CreateGitProcessStartInfo(_currentDir, _env);
					monitor?.SetCancellationAction(delegate
					{
						process.SendSigintSignal();
					});
					try
					{
						process.Start();
						Task task = Task.Run(delegate
						{
							StreamReader standardOutput = process.StandardOutput;
							while (true)
							{
								string text2 = standardOutput.ReadLine();
								if (text2 == null)
								{
									break;
								}
								stdoutPipeHandler(text2);
							}
						});
						Task task2 = new Task(delegate
						{
							StreamReader standardError = process.StandardError;
							while (true)
							{
								string text = standardError.ReadLine();
								if (text == null)
								{
									break;
								}
								stderrPipeHandler(text);
							}
						}, TaskCreationOptions.LongRunning);
						task2.Start();
						task.Wait();
						task2.Wait();
						if (process.ExitCode != 0)
						{
							Log.Warn("Git request failed '" + _command?.ArgumentsString + "'");
						}
						return ExecuteWithCallbackResponse.Create(process.ExitCode);
					}
					catch (Exception ex)
					{
						Log.Error("Failed to execute " + ServiceLocator.GitEnvironment.GitPath, ex);
						return ExecuteWithCallbackResponse.Failure(new UnhandledExceptionError(ex));
					}
					finally
					{
						monitor?.SetCancellationAction(null);
					}
				}
				finally
				{
					if (process != null)
					{
						((IDisposable)process).Dispose();
					}
				}
			}
		}

		public ShellRequestBinaryResult ExecuteBinary(JobMonitor cancellationToken = null, bool silent = false)
		{
			if (!File.Exists(ServiceLocator.GitEnvironment.GitPath))
			{
				return new ShellRequestBinaryResult(-1, null, "Cannot find git instance at: '" + ServiceLocator.GitEnvironment.GitPath + "'");
			}
			using (new Benchmarker(_command?.ArgumentsString ?? ""))
			{
				Process process = new Process();
				try
				{
					process.StartInfo = CreateGitProcessStartInfo(_currentDir, _env);
					cancellationToken?.SetCancellationAction(delegate
					{
						process.SendSigintSignal();
					});
					try
					{
						process.Start();
						MemoryStream memoryStream = new MemoryStream();
						string error = string.Empty;
						Task task = Task.Run(delegate
						{
							error = process.StandardError.ReadToEnd();
						});
						if (_stdin != null && _stdin.Length != 0)
						{
							process.StandardInput.BaseStream.Write(_stdin, 0, _stdin.Length);
							process.StandardInput.Close();
						}
						process.StandardOutput.BaseStream.CopyTo(memoryStream);
						task.Wait();
						if (process.ExitCode != 0 && !silent)
						{
							Log.Warn("Git request failed '" + _command?.ArgumentsString + "':\n" + error);
						}
						return new ShellRequestBinaryResult(process.ExitCode, memoryStream, error.ToString());
					}
					catch (Exception ex)
					{
						Log.Error(ServiceLocator.GitEnvironment.GitPath, ex);
						return new ShellRequestBinaryResult(-1, null, ex.ToString());
					}
					finally
					{
						cancellationToken?.SetCancellationAction(null);
					}
				}
				finally
				{
					if (process != null)
					{
						((IDisposable)process).Dispose();
					}
				}
			}
		}

		public GitRequestResult ExecuteLong(Action<string> outputPipeHandler, Action<string> errorPipeHandler, JobMonitor monitor, int retryCount)
		{
			int num = 0;
			GitRequestResult gitRequestResult;
			do
			{
				num++;
				if (num > 1)
				{
					monitor.AppendOutputLine("\nRepository is locked. Retrying...\n");
					Thread.Sleep(num * 500);
					monitor.Append(_path, _command);
				}
				gitRequestResult = ExecuteLong(outputPipeHandler, errorPipeHandler, monitor);
				if (gitRequestResult.Success)
				{
					return gitRequestResult;
				}
			}
			while (GitCommandError.RepositoryIsLocked.Test(gitRequestResult.Stderr) && num <= retryCount);
			return gitRequestResult;
		}

		public GitRequestResult Execute(int retryCount, bool silent = false)
		{
			int num = 0;
			GitRequestResult gitRequestResult;
			do
			{
				num++;
				if (num > 1)
				{
					Thread.Sleep(num * 500);
				}
				gitRequestResult = Execute(silent);
				if (gitRequestResult.Success)
				{
					return gitRequestResult;
				}
			}
			while (GitCommandError.RepositoryIsLocked.Test(gitRequestResult.Stderr) && num <= retryCount);
			return gitRequestResult;
		}

		private ProcessStartInfo CreateGitProcessStartInfo([Null] string currentDir, [Null] (string, string)[] environmentVariables = null)
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo
			{
				FileName = ServiceLocator.GitEnvironment.GitPath,
				Arguments = (_command?.ArgumentsString ?? ""),
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				WorkingDirectory = currentDir,
				ErrorDialog = false,
				CreateNoWindow = true,
				RedirectStandardInput = true,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8
			};
			if (currentDir != null)
			{
				processStartInfo.WorkingDirectory = currentDir;
			}
			processStartInfo.EnvironmentVariables["SSH_ASKPASS_REQUIRE"] = "force";
			processStartInfo.EnvironmentVariables[Consts.Env.AskPass] = ServiceLocator.AppContext.ForkCredentialHelperPath;
			processStartInfo.EnvironmentVariables[Consts.Env.ForkPlusProcessId] = ServiceLocator.AppContext.ProcessId.ToString();
			if (currentDir != null)
			{
				processStartInfo.EnvironmentVariables["FORK_REPOSITORY_PATH"] = currentDir;
			}
			string text = GitSshCommand();
			if (text != null)
			{
				processStartInfo.EnvironmentVariables["GIT_SSH_COMMAND"] = text;
			}
			if (ServiceLocator.UserSettings.VerboseGitOutput)
			{
				processStartInfo.EnvironmentVariables["GIT_TRACE"] = "1";
				processStartInfo.EnvironmentVariables["GIT_TRACE_CURL"] = "1";
				processStartInfo.EnvironmentVariables["GIT_TRACE_PACKFILE"] = "1";
				processStartInfo.EnvironmentVariables["GIT_TRACE_PERFORMANCE"] = "1";
			}
			if (environmentVariables != null)
			{
				for (int i = 0; i < environmentVariables.Length; i++)
				{
					(string, string) tuple = environmentVariables[i];
					processStartInfo.EnvironmentVariables[tuple.Item1] = tuple.Item2;
				}
			}
			return processStartInfo;
		}

		private static string[] CreateDefaultEnv([Null] string currentDir, [Null] (string, string)[] additionalEnv)
		{
			List<string> list = new List<string>(2 * (4 + additionalEnv?.Length).GetValueOrDefault());
			list.Add("SSH_ASKPASS_REQUIRE");
			list.Add("force");
			list.Add("SSH_ASKPASS");
			list.Add(ServiceLocator.AppContext.ForkCredentialHelperPath);
			list.Add(Consts.Env.ForkPlusProcessId);
			list.Add(ServiceLocator.AppContext.ProcessIdString);
			if (currentDir != null)
			{
				list.Add("FORK_REPOSITORY_PATH");
				list.Add(currentDir);
			}
			string text = GitSshCommand();
			if (text != null)
			{
				list.Add("GIT_SSH_COMMAND");
				list.Add(text);
			}
			if (additionalEnv != null)
			{
				for (int i = 0; i < additionalEnv.Length; i++)
				{
					(string, string) tuple = additionalEnv[i];
					list.Add(tuple.Item1);
					list.Add(tuple.Item2);
				}
			}
			if (ServiceLocator.UserSettings.VerboseGitOutput)
			{
				list.Add("GIT_TRACE");
				list.Add("1");
				list.Add("GIT_TRACE_CURL");
				list.Add("1");
				list.Add("GIT_TRACE_PACKFILE");
				list.Add("1");
				list.Add("GIT_TRACE_PERFORMANCE");
				list.Add("1");
			}
			return list.ToArray();
		}

		[Null]
		private static string GitSshCommand()
		{
			StringBuilder stringBuilder = new StringBuilder(1024);
			if (ServiceLocator.UserSettings.VerboseGitOutput)
			{
				stringBuilder.Append(" -vvv");
			}
			string[] sshKeys = ServiceLocator.UserSettings.SshKeys;
			if (sshKeys != null && sshKeys.Length != 0)
			{
				StringBuilder stringBuilder2 = new StringBuilder(1024);
				string[] array = sshKeys;
				foreach (string path in array)
				{
					stringBuilder2.Append("-i '");
					stringBuilder2.Append(PathHelper.NormalizeUnix(path));
					stringBuilder2.Append("' ");
				}
				stringBuilder.Append($" {stringBuilder2}-F '/dev/null'");
			}
			if (stringBuilder.Length == 0)
			{
				return null;
			}
			return $"ssh{stringBuilder}";
		}
	}
}
