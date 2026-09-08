using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ForkPlus.Git.Interaction;
using ForkPlus.Settings;

namespace ForkPlus.Shell.Interaction
{
	public class ShellRequest
	{
		private readonly GitCommand _command;

		[Null]
		public string WorkingDirectory { get; }

		public string FilePath { get; }

		/// <summary>
		/// 写入子进程标准输入的内容（可选）。
		/// 设置后 Execute 会重定向 stdin、写入该内容并关闭流——用于
		/// <c>git-ai checkpoint agent-v1 --hook-input stdin</c> 这类从 stdin 接收 JSON 的命令。
		/// 未设置（null）时行为与原来完全一致。
		/// </summary>
		[Null]
		public string StandardInput { get; set; }

		public ShellRequest([Null] string workingDirectory, string filePath, string[] arguments)
		{
			WorkingDirectory = workingDirectory;
			FilePath = filePath;
			_command = new GitCommand(arguments);
		}

		public GitRequestResult Execute()
		{
			return Execute(0);
		}

		/// <summary>
		/// 执行命令，可选超时（毫秒）。
		/// 外部工具（如 git-ai）可能因 daemon 冷启动或大仓库而长时间不返回，
		/// 超时后强制结束进程并返回失败结果（exit code -1、stderr 标注超时），调用方得以优雅降级。
		/// </summary>
		/// <param name="timeoutMilliseconds">超时毫秒数，小于等于 0 表示不设超时（与原 Execute 行为一致）。</param>
		public GitRequestResult Execute(int timeoutMilliseconds)
		{
			string argumentsString = _command.ArgumentsString;
			Benchmarker benchmarker = new Benchmarker("Running '" + FilePath + " " + argumentsString + "'");
			Log.Info("Running '" + FilePath + " " + argumentsString + "'");
			Process process = new Process();
			try
			{
				process.StartInfo = CreateProcessStartInfo(StandardInput != null);
				process.Start();
				// 先启动 stdout/stderr 读取再写 stdin：若子进程输出先填满管道缓冲区而无人读取，
				// 会停止消费 stdin 导致 Write 死锁（transcript JSON 可达数十 KB）
				Task<string> stdoutTask = Task.Run(delegate
				{
					return process.StandardOutput.ReadToEnd();
				});
				Task<string> stderrTask = Task.Run(delegate
				{
					return process.StandardError.ReadToEnd();
				});
				if (StandardInput != null)
				{
					try
					{
						process.StandardInput.Write(StandardInput);
						process.StandardInput.Close();
					}
					catch (Exception ex2)
					{
						// 子进程可能提前退出（如 git-ai 版本过旧不认识 agent-v1 preset），写 stdin 失败不影响结果读取
						Log.Warn("Failed to write standard input for '" + FilePath + " " + argumentsString + "': " + ex2.Message);
					}
				}
				bool exited;
				if (timeoutMilliseconds > 0)
				{
					exited = process.WaitForExit(timeoutMilliseconds);
				}
				else
				{
					process.WaitForExit();
					exited = true;
				}
				if (!exited)
				{
					Log.Warn("Shell request '" + FilePath + " " + argumentsString + "' timed out after " + timeoutMilliseconds + "ms, killing process");
					TryKill(process);
					return new GitRequestResult(-1, "", "Command timed out after " + timeoutMilliseconds + "ms: '" + FilePath + " " + argumentsString + "'");
				}
				string text = stdoutTask.Result;
				string text2 = stderrTask.Result;
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

		/// <summary>尽力结束进程。进程已退出或无权限时静默忽略。</summary>
		private static void TryKill(Process process)
		{
			try
			{
				if (process != null && !process.HasExited)
				{
					process.Kill();
				}
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to kill timed-out process: " + ex.Message);
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
			// Migration note：git 可执行文件名跨平台（原 "git.exe" 后缀判断在 Unix 失效，credential helper 不生效）。
			if (SystemEnvironment.IsGitExecutable(FilePath))
			{
				text = string.Join(" ", App.OverrideCredentialHelper) + " " + text;
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
			processStartInfo.EnvironmentVariables[Consts.Env.AskPass] = App.ForkCredentialHelperPath;
			processStartInfo.EnvironmentVariables[Consts.Env.ForkPlusProcessId] = App.ProcessId.ToString();
			if (WorkingDirectory != null)
			{
				processStartInfo.EnvironmentVariables["FORK_REPOSITORY_PATH"] = WorkingDirectory;
			}
			string[] sshKeys = ForkPlusSettings.Default.SshKeys;
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
			// 凭据收编（Layer B）：环境级注入（GIT_ASKPASS / GIT_TERMINAL_PROMPT / GIT_CONFIG_*）。
			// FilePath 可能是 git 可执行文件（上方 IsGitExecutable 分支已拼 Layer A 的 -c 覆盖链），
			// env 形式随进程树传播，git-mm / submodule 等内部再拉起的 git 子进程继承同一收编语义。
			GitCredentialEnv.ApplyToProcessStartInfo(processStartInfo);
			return processStartInfo;
		}
	}
}
