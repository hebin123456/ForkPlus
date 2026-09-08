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
using ForkPlus.Settings;

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
				string path = _path ?? App.GitPath;
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
					string path = _path ?? App.GitPath;
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
			if (!File.Exists(App.GitPath))
			{
				return new GitRequestResult(-1, string.Empty, "Cannot find git instance at: '" + App.GitPath + "'");
			}
			using (new Benchmarker(_command?.ArgumentsString ?? ""))
			{
				Process process = new Process();
				try
				{
					process.StartInfo = CreateGitProcessStartInfo(_currentDir, _env);
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
					// Migration note：Unix 上 ReadToEnd 返回（EOF）≠ 进程已退出，此刻读 ExitCode 抛
					// InvalidOperationException: Process must exit before requested information
					// can be determined（首启 SetGlobalUserIdentity 全失败实证）。管道已读完，
					// WaitForExit 不会死锁。Windows 上同样安全（幂等）。
					process.WaitForExit();
					if (process.ExitCode != 0 && !silent)
					{
						Log.Warn("Git request failed '" + _command?.ArgumentsString + "':\n" + error);
					}
					return new GitRequestResult(process.ExitCode, text.ToString(), error.ToString());
					}
					catch (Exception ex)
					{
						Log.Error(App.GitPath, ex);
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
			if (!File.Exists(App.GitPath))
			{
				return new GitRequestResult(-1, string.Empty, "Cannot find git instance at: '" + App.GitPath + "'");
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
					// Migration note：同上，Unix EOF≠退出，WaitForExit 后再读 ExitCode。
					process.WaitForExit();
					if (process.ExitCode != 0 && !silent)
						{
							Log.Warn("Git request failed '" + _command?.ArgumentsString + "':\n" + error);
						}
						return new GitRequestResult(process.ExitCode, text.ToString(), error.ToString());
					}
					catch (Exception ex)
					{
						Log.Error(App.GitPath, ex);
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
			if (!File.Exists(App.GitPath))
			{
				return new GitRequestResult(-1, string.Empty, "Cannot find git instance at: '" + App.GitPath + "'");
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
					// Migration note：同上，Unix EOF≠退出，WaitForExit 后再读 ExitCode。
					process.WaitForExit();
					if (process.ExitCode != 0)
					{
						Log.Warn("Git request failed '" + _command?.ArgumentsString + "':\n" + text);
					}
					return new GitRequestResult(process.ExitCode, stdout, text);
					}
					catch (Exception ex)
					{
						Log.Error(App.GitPath, ex);
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
			if (!File.Exists(App.GitPath))
			{
				return ExecuteWithCallbackResponse.Failure(new GenericError("Cannot find git instance at: '" + App.GitPath + "'"));
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
					// Migration note：同上，Unix EOF≠退出，WaitForExit 后再读 ExitCode。
					process.WaitForExit();
					if (process.ExitCode != 0)
					{
						Log.Warn("Git request failed '" + _command?.ArgumentsString + "'");
					}
					return ExecuteWithCallbackResponse.Create(process.ExitCode);
					}
					catch (Exception ex)
					{
						Log.Error("Failed to execute " + App.GitPath, ex);
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
			if (!File.Exists(App.GitPath))
			{
				return new ShellRequestBinaryResult(-1, null, "Cannot find git instance at: '" + App.GitPath + "'");
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
					// Migration note：同上，Unix EOF≠退出，WaitForExit 后再读 ExitCode。
					process.WaitForExit();
					if (process.ExitCode != 0 && !silent)
					{
						Log.Warn("Git request failed '" + _command?.ArgumentsString + "':\n" + error);
					}
					return new ShellRequestBinaryResult(process.ExitCode, memoryStream, error.ToString());
					}
					catch (Exception ex)
					{
						Log.Error(App.GitPath, ex);
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
				FileName = App.GitPath,
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
			processStartInfo.EnvironmentVariables[Consts.Env.AskPass] = App.ForkCredentialHelperPath;
			processStartInfo.EnvironmentVariables[Consts.Env.ForkPlusProcessId] = App.ProcessId.ToString();
			if (currentDir != null)
			{
				processStartInfo.EnvironmentVariables["FORK_REPOSITORY_PATH"] = currentDir;
			}
			string text = GitSshCommand();
			if (text != null)
			{
				processStartInfo.EnvironmentVariables["GIT_SSH_COMMAND"] = text;
			}
			if (ForkPlusSettings.Default.VerboseGitOutput)
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
			// 凭据收编（Layer B）：环境级注入（GIT_ASKPASS / GIT_TERMINAL_PROMPT / GIT_CONFIG_*）。
			// 必须放在 environmentVariables 应用之后：GIT_CONFIG_COUNT 读取的是合并后的值，
			// 我们的条目顺延编号不覆盖调用方注入。-c 不随进程树传播，env 形式才能让
			// git-mm / submodule 等内部再拉起的 git 子进程继承同一收编语义。
			GitCredentialEnv.ApplyToProcessStartInfo(processStartInfo);
			// git-mm 子命令可见性（2026-09-07，"GUI 报 git: 'mm' is not a git command"）：
			// git 查找 mm 走"自身 exec-path + 进程 PATH"，GUI 的自带 git 实例 exec-path 与系统 git
			// 不同、桌面启动的进程 PATH 又可能缺用户 bin——解析到 git-mm 后把其目录前置进 git
			// 子进程 PATH。幂等（目录已在 PATH 则不注入）；自带 exec-path 优先级更高，既有命令不受影响。
			string pathWithGitMm = App.PrependGitMmDirectoryToPath(
				processStartInfo.EnvironmentVariables.ContainsKey("PATH") ? processStartInfo.EnvironmentVariables["PATH"] : null);
			if (pathWithGitMm != null)
			{
				processStartInfo.EnvironmentVariables["PATH"] = pathWithGitMm;
			}
			return processStartInfo;
		}

		private static string[] CreateDefaultEnv([Null] string currentDir, [Null] (string, string)[] additionalEnv)
		{
			// 容量基数 11 = SSH_ASKPASS_REQUIRE/SSH_ASKPASS/FORK_PLUS_PROCESS_ID/FORK_REPOSITORY_PATH
			// 4 项 + Layer B 凭据收编 7 项（GitCredentialEnv.BuildAllPairs 对数）。
			List<string> list = new List<string>(2 * (11 + additionalEnv?.Length).GetValueOrDefault());
			list.Add("SSH_ASKPASS_REQUIRE");
			list.Add("force");
			list.Add("SSH_ASKPASS");
			list.Add(App.ForkCredentialHelperPath);
			list.Add(Consts.Env.ForkPlusProcessId);
			list.Add(App.ProcessIdString);
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
			if (ForkPlusSettings.Default.VerboseGitOutput)
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
			// 凭据收编（Layer B）：环境级注入（GIT_ASKPASS / GIT_TERMINAL_PROMPT / GIT_CONFIG_*）。
			// 放在 additionalEnv 之后：FindConfigCount 从数组尾部扫描，读取的是"父环境 +
			// additionalEnv"合并后的有效 COUNT，我们的条目顺延编号不覆盖既有注入。
			// env 形式随进程树传播——git-mm / submodule 等内部再拉起的 git 子进程继承同一收编语义。
			int configStartIndex = GitCredentialEnv.FindConfigCount(list);
			(string, string)[] credentialPairs = GitCredentialEnv.BuildAllPairs(configStartIndex);
			for (int j = 0; j < credentialPairs.Length; j++)
			{
				list.Add(credentialPairs[j].Item1);
				list.Add(credentialPairs[j].Item2);
			}
			// git-mm 子命令可见性（同 CreateGitProcessStartInfo 处注释）：Bt 路径的 env 数组是
			// 叠加在继承环境之上的增量覆盖集——这里写入"git-mm 目录 + 本进程 PATH"的完整合并值。
			string pathWithGitMm = App.PrependGitMmDirectoryToPath(Environment.GetEnvironmentVariable("PATH"));
			if (pathWithGitMm != null)
			{
				list.Add("PATH");
				list.Add(pathWithGitMm);
			}
			return list.ToArray();
		}

		[Null]
		private static string GitSshCommand()
		{
			StringBuilder stringBuilder = new StringBuilder(1024);
			if (ForkPlusSettings.Default.VerboseGitOutput)
			{
				stringBuilder.Append(" -vvv");
			}
			string[] sshKeys = ForkPlusSettings.Default.SshKeys;
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
