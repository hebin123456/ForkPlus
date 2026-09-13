using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace ForkPlus.AutoUpdater
{
	/// <summary>
	/// AutoUpdater 入口：主程序（ForkPlus.exe）的自动更新子进程。
	/// 命令行（全部键值对，顺序无关）：
	///   --url &lt;zip下载地址&gt;          GitHub release 平台包直链
	///   --install-dir &lt;安装目录&gt;      要替换的程序目录
	///   --restart-command &lt;主程序路径&gt; 更新完成后要重启的主程序 exe
	///   --pipe-pid &lt;主程序pid&gt;        进度管道名后缀（Fork_Pipe{pid}_Update）
	///   --wait-pid &lt;主程序pid&gt;        等待退出的主程序 pid（替换前必须退出）
	///   --no-restart                  只安装不重启（测试用）
	///   --wait-timeout-ms &lt;毫秒&gt;      等主程序退出的超时（默认 60000，测试用）
	///   --reset-settings              重置当前版本模式：文件替换成功后删除主程序设置
	///   --settings-file &lt;路径&gt;         要删除的 settings.json（缺省按约定位置推导；
	///                                 与 --reset-settings 配套，主程序生产路径会显式传入）
	/// 主程序侧约定：updater 总是自临时副本目录运行（主程序启动前已复制），
	/// 因此安装目录内没有被 updater 自身锁定的文件。退出码：0 成功 / 1 失败 /
	/// 130 被取消（进程被 Kill 时无退出码，此码用于参数级早退路径的语义完整性）。
	/// </summary>
	internal static class Program
	{
		/// <summary>更新工作根目录（临时区）：zip / 解压 / backup 都在其下的会话子目录里。</summary>
		internal static readonly string WorkRoot = Path.Combine(Path.GetTempPath(), "ForkPlusUpdate");

		private const int ExitSuccess = 0;

		private const int ExitError = 1;

		private const int ExitCancelled = 130;

		private static int Main(string[] args)
		{
			try
			{
				UpdateOptions options = UpdateOptions.Parse(args);
				if (options == null)
				{
					Console.Error.WriteLine("Usage: ForkPlus.AutoUpdater --url <zip url> --install-dir <dir> [--restart-command <exe>] [--pipe-pid <pid>] [--wait-pid <pid>] [--no-restart] [--wait-timeout-ms <ms>] [--reset-settings] [--settings-file <path>]");
					return ExitError;
				}
				return Run(options);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("AutoUpdater fatal: " + ex.Message);
				return ExitError;
			}
		}

		internal static int Run(UpdateOptions options)
		{
			// 历史残留清理（上次更新被取消/失败的 zip 与解压目录），best effort 不阻断
			UpdateInstaller.CleanupStaleWorkDirs(WorkRoot, TimeSpan.FromDays(1));

			string sessionDir = Path.Combine(WorkRoot, "u-" + Guid.NewGuid().ToString("N").Substring(0, 12));
			Directory.CreateDirectory(sessionDir);

			using (UpdateProgressPipeClient progress = new UpdateProgressPipeClient())
			{
				// 主程序会先起好监听再 spawn 本进程；连不上（超时 5s）则降级为无进度上报
				if (options.PipePid > 0)
				{
					progress.TryConnect(options.PipeName, 5000);
				}
				ReportProgress(progress, UpdateMessageProtocol.Phase(UpdateMessageProtocol.PhaseDownloading));
				try
				{
					// ① 下载
					UpdateDownloader downloader = new UpdateDownloader(options.Url, Path.Combine(sessionDir, "update.zip"), minimumBytes: 1048576L);
					bool downloaded = downloader.Download(
						(received, total) => ReportProgress(progress, UpdateMessageProtocol.Download(received, total)),
						CancellationToken.None,
						maxAttempts: 3);
					if (!downloaded)
					{
						// 参数/早退路径的取消语义（正常取消 = 主程序 Kill，进程直接死，不会走到这里）
						return ExitCancelled;
					}

					// ② 解压
					ReportProgress(progress, UpdateMessageProtocol.Phase(UpdateMessageProtocol.PhaseExtracting));
					UpdateInstaller installer = new UpdateInstaller(options.InstallDir, options.RestartCommand);
					string newFilesDir = installer.Extract(downloader.ZipPath, Path.Combine(sessionDir, "staging"));

					// ③ 等主程序退出（主程序收到 waiting-exit 自行关闭；这里兜底等 pid）
					ReportProgress(progress, UpdateMessageProtocol.Phase(UpdateMessageProtocol.PhaseWaitingExit));
					if (!installer.WaitForMainProcessExit(options.WaitPid, options.WaitTimeoutMs))
					{
						throw new InvalidOperationException("Main application did not exit within " + options.WaitTimeoutMs + "ms; aborting to avoid replacing locked files");
					}

					// ④ 备份替换
					ReportProgress(progress, UpdateMessageProtocol.Phase(UpdateMessageProtocol.PhaseReplacing));
					string backupDir = Path.Combine(sessionDir, "backup");
					installer.Replace(newFilesDir, backupDir);

					// ④.5 重置设置（--reset-settings，"重置此版本"流）：仅在文件替换成功后
					// 删除——更新失败路径（下载/解压/回滚）设置必须原样保留。best effort：
					// 删除失败（占用等）不阻断重启，仅 stderr 记录。
					if (options.ResetSettings)
					{
						UpdateInstaller.DeleteSettingsFile(options.SettingsFile);
					}

					// ⑤ 重启
					ReportProgress(progress, UpdateMessageProtocol.Phase(UpdateMessageProtocol.PhaseRestarting));
					if (!options.NoRestart)
					{
						installer.RestartMainApp();
					}
					ReportProgress(progress, UpdateMessageProtocol.Phase(UpdateMessageProtocol.PhaseDone));

					// ⑥ 会话目录清理（zip/解压已消费，成功路径的 backup 不再需要；失败留下，
					// 由下次启动的 CleanupStaleWorkDirs 收尾）
					TryDeleteDir(sessionDir);
					return ExitSuccess;
				}
				catch (Exception ex)
				{
					ReportProgress(progress, UpdateMessageProtocol.Error(ex.Message));
					Console.Error.WriteLine("AutoUpdater failed: " + ex.Message);
					return ExitError;
				}
			}
		}

		private static void ReportProgress(UpdateProgressPipeClient pipe, string message)
		{
			// 进度上报 best effort：管道写失败（主程序已退出/从未监听）已在客户端内部降级
			try
			{
				pipe.Write(message);
			}
			catch (Exception)
			{
			}
		}

		private static void TryDeleteDir(string dir)
		{
			try
			{
				if (Directory.Exists(dir))
				{
					Directory.Delete(dir, recursive: true);
				}
			}
			catch (Exception)
			{
				// 残留下次启动由 CleanupStaleWorkDirs 收
			}
		}

		/// <summary>命令行参数解析（--key value 键值对，顺序无关；未知键报错）。</summary>
		internal sealed class UpdateOptions
		{
			public string Url { get; private set; }

			public string InstallDir { get; private set; }

			public string RestartCommand { get; private set; }

			public int PipePid { get; private set; }

			public int WaitPid { get; private set; }

			public bool NoRestart { get; private set; }

			public int WaitTimeoutMs { get; private set; } = UpdateInstaller.WaitExitTimeoutMs;

			/// <summary>"重置此版本"模式：文件替换成功后删除主程序设置文件。</summary>
			public bool ResetSettings { get; private set; }

			/// <summary>要删除的 settings.json 路径（--reset-settings 配套）。空 = 按约定位置推导。</summary>
			public string SettingsFile { get; private set; }

			/// <summary>解析失败（缺必填项/未知键/值非法）返回 null，由 Main 打用法。</summary>
			public static UpdateOptions Parse(string[] args)
			{
				UpdateOptions options = new UpdateOptions();
				for (int i = 0; i < args.Length; i++)
				{
					string key = args[i];
					string value = (i + 1 < args.Length) ? args[i + 1] : null;
					switch (key)
					{
						case "--url":
							if (value == null || !value.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return null;
							options.Url = value;
							i++;
							break;
						case "--install-dir":
							if (string.IsNullOrWhiteSpace(value)) return null;
							options.InstallDir = value;
							i++;
							break;
						case "--restart-command":
							if (string.IsNullOrWhiteSpace(value)) return null;
							options.RestartCommand = value;
							i++;
							break;
						case "--pipe-pid":
							if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pipePid) || pipePid <= 0) return null;
							options.PipePid = pipePid;
							i++;
							break;
						case "--wait-pid":
							if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int waitPid) || waitPid <= 0) return null;
							options.WaitPid = waitPid;
							i++;
							break;
						case "--no-restart":
						options.NoRestart = true;
						break;
					case "--reset-settings":
						options.ResetSettings = true;
						break;
					case "--settings-file":
						if (string.IsNullOrWhiteSpace(value)) return null;
						options.SettingsFile = value;
						i++;
						break;
						case "--wait-timeout-ms":
							if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int timeoutMs) || timeoutMs < 0) return null;
							options.WaitTimeoutMs = timeoutMs;
							i++;
							break;
						default:
							return null;
					}
				}
				if (string.IsNullOrEmpty(options.Url) || string.IsNullOrEmpty(options.InstallDir))
				{
					return null;
				}
				return options;
			}

			/// <summary>进度管道名（与主程序 NamedPipeHelper.CreatePipeName("Update", pid) 同构）。</summary>
			public string PipeName => "Fork_Pipe" + PipePid + "_Update";
		}
	}
}
