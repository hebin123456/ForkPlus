using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using ForkPlus.IO.Ipc;

namespace ForkPlus
{
	/// <summary>
	/// 自动更新包地址解析：优先用 GitHub API 返回的平台资产直链（UpdateChecker 已按
	/// 当前平台挑好），非 zip（资产缺失回退到了 Release 页）时按版本号+平台构造
	/// 规则下载地址（releases/download/v{版本}/ForkPlus-{版本}-{平台}.zip），
	/// 两者都不可用时返回 null（调用方回退浏览器打开）。
	/// </summary>
	public static class AutoUpdatePackageUrl
	{
		/// <summary>GitHub release 资产的确定性下载前缀（tag 命名 v&lt;版本&gt;）。</summary>
		internal const string ReleaseDownloadBase = "https://github.com/hebin123456/ForkPlus/releases/download/";

		/// <summary>
		/// 解析可用于自动下载的 zip 直链。url 为 http(s) 且以 .zip 结尾时原样返回；
		/// 否则按 version+platformId 构造规则地址；version 为空返回 null。
		/// platformId 由调用方传入（测试可注入；生产用 UpdateChecker.GetCurrentPlatformId）。
		/// </summary>
		public static string Resolve(string url, string version, string platformId)
		{
			if (!string.IsNullOrWhiteSpace(url)
				&& url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
				&& url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
			{
				return url;
			}
			if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(platformId))
			{
				return null;
			}
			string normalized = UpdateChecker.NormalizeVersion(version);
			if (string.IsNullOrEmpty(normalized))
			{
				return null;
			}
			return ReleaseDownloadBase + "v" + normalized + "/ForkPlus-" + normalized + "-" + platformId + ".zip";
		}

		/// <summary>生产入口：用 UpdateInfo（已带平台资产直链或回退页）+ 当前真实平台解析。</summary>
		public static string Resolve(UpdateInfo info)
		{
			if (info == null)
			{
				return null;
			}
			return Resolve(info.DownloadUrl, info.LatestVersion, UpdateChecker.GetCurrentPlatformId());
		}
	}

	/// <summary>AutoUpdater 进度消息（主程序侧解析结果）。</summary>
	public sealed class AutoUpdateProgress
	{
		/// <summary>下载阶段：已收/总字节数；total &lt; 0 = 未知长度（不确定进度）。</summary>
		public long ReceivedBytes { get; }

		public long TotalBytes { get; }

		/// <summary>当前阶段名（与 AutoUpdater 的 UpdateMessageProtocol 阶段一致）。</summary>
		public string Phase { get; }

		/// <summary>失败原因（仅失败消息）。</summary>
		public string ErrorMessage { get; }

		public bool IsError => ErrorMessage != null;

		private AutoUpdateProgress(long received, long total, string phase, string error)
		{
			ReceivedBytes = received;
			TotalBytes = total;
			Phase = phase;
			ErrorMessage = error;
		}

		/// <summary>
		/// 解析一条管道消息。与 ForkPlus.AutoUpdater.UpdateMessageProtocol 格式一致
		///（主程序与 updater 分属两个程序集，各持一份格式副本，本方法即解析语义的
		/// 唯一实现，E2E 与单元用例共同锁定）。
		/// </summary>
		public static AutoUpdateProgress Parse(string message)
		{
			if (string.IsNullOrEmpty(message))
			{
				return null;
			}
			if (message.StartsWith("download:", StringComparison.Ordinal))
			{
				string[] parts = message.Substring("download:".Length).Split(':');
				if (parts.Length == 2
					&& long.TryParse(parts[0], out long received)
					&& long.TryParse(parts[1], out long total))
				{
					return new AutoUpdateProgress(received, total, "downloading", null);
				}
				return null;
			}
			if (message.StartsWith("phase:", StringComparison.Ordinal))
			{
				return new AutoUpdateProgress(0, 0, message.Substring("phase:".Length), null);
			}
			if (message.StartsWith("error:", StringComparison.Ordinal))
			{
				return new AutoUpdateProgress(0, 0, null, message.Substring("error:".Length));
			}
			return null;
		}

		/// <summary>本进度是否表示"等待主程序退出"（收到即触发应用关闭，交由 updater 替换）。</summary>
		public bool IsWaitingExit => Phase == "waiting-exit";
	}

	/// <summary>
	/// 主程序侧自动更新驱动器：把安装目录里的 ForkPlus.AutoUpdater 复制到临时目录
	///（updater 替换安装目录时不能锁住自身）、起进度管道监听（Fork_Pipe{pid}_Update）、
	/// 启动 updater 进程、解析进度消息并转发到 UI 线程事件。
	/// 取消 = Kill updater 进程（仅下载阶段允许，窗口侧守卫）。
	/// </summary>
	public sealed class AutoUpdateRunner : IDisposable
	{
		/// <summary>管道名后缀（与 updater 侧 "--pipe-pid" 计算的 Fork_Pipe{pid}_Update 一致）。</summary>
		internal const string PipeNameSuffix = "Update";

		private readonly IpcServer _pipeServer;

		private readonly string _updaterExeSourceDir;

		private readonly string _installDir;

		private readonly string _restartCommand;

		private readonly int _waitPid;

		private readonly bool _noRestart;

		private readonly bool _resetSettings;

		private readonly string _settingsFile;

		private readonly string _tempLaunchDir;

		private Process _updaterProcess;

		private bool _disposed;

		/// <summary>Exited 事件只发一次（管道断开与进程看门狗两条路径都触达）。</summary>
		private int _exitRaised;

		/// <summary>updater 是否仍在运行（已启动且未退出）。</summary>
		public bool IsRunning => _updaterProcess != null && !_updaterProcess.HasExited;

		/// <summary>当前是否处于下载阶段（唯一允许取消的阶段）。</summary>
		public bool IsDownloading { get; private set; }

		/// <summary>updater 进程退出码（Exited 事件后有效；null = 尚未退出或被 Kill）。</summary>
		public int? ExitCode => (_updaterProcess != null && _updaterProcess.HasExited) ? _updaterProcess.ExitCode : default(int?);

		/// <summary>进度消息（UI 线程回调；含下载字节数/阶段/失败）。</summary>
		public event Action<AutoUpdateProgress> Progress;

		/// <summary>updater 进程退出（UI 线程回调；Cancel/失败/完成都会走到）。</summary>
		public event Action Exited;

		/// <summary>生产构造：安装目录 = 当前程序目录，等待退出 = 本进程，更新完自动重启。</summary>
		public AutoUpdateRunner()
			: this(AppContext.BaseDirectory, AppContext.BaseDirectory, Environment.ProcessPath, waitPid: App.ProcessId, noRestart: false)
		{
		}

		/// <summary>
		/// 生产构造（"重置此版本"流）：与默认构造同目标（重下当前版本包 → 替换安装目录 →
		/// 自动重启），resetSettings=true 时附加设置重置语义——由 updater 在文件替换成功后
		/// 删除 settings.json（App.ForkDirectoryPath 下，更新失败路径不动设置）。
		/// </summary>
		public static AutoUpdateRunner CreateForVersionReset(bool resetSettings)
		{
			return new AutoUpdateRunner(
				AppContext.BaseDirectory, AppContext.BaseDirectory, Environment.ProcessPath,
				waitPid: App.ProcessId, noRestart: false,
				resetSettings: resetSettings,
				settingsFile: resetSettings ? Path.Combine(App.ForkDirectoryPath, "settings.json") : null);
		}

		/// <summary>
		/// 生产/测试通用构造。updaterExeSourceDir：从哪里复制 ForkPlus.AutoUpdater.exe；
		/// installDir：要替换的目录；restartCommand：替换后重启的 exe 路径；
		/// waitPid：替换前等待退出的进程（生产 = 主程序自身）；noRestart：只安装不重启（测试）；
		/// resetSettings/settingsFile：文件替换成功后删除的设置文件（"重置此版本"流，
		/// settingsFile 仅在 resetSettings 时随命令行传给 updater）。
		/// </summary>
		internal AutoUpdateRunner(string updaterExeSourceDir, string installDir, string restartCommand, int waitPid, bool noRestart, bool resetSettings = false, string settingsFile = null)
		{
			_updaterExeSourceDir = updaterExeSourceDir;
			_installDir = installDir;
			_restartCommand = restartCommand;
			_waitPid = waitPid;
			_noRestart = noRestart;
			_resetSettings = resetSettings;
			_settingsFile = settingsFile;
			_tempLaunchDir = Path.Combine(Path.GetTempPath(), "ForkPlusUpdate", "launch-" + Guid.NewGuid().ToString("N").Substring(0, 12));
			// 进度管道监听：先起好再 spawn updater（updater 连不上会静默降级，因此顺序上
			// 必须先 listen 后 start）。消息循环在 IpcServer 自有线程，回调统一转 UI 线程。
			_pipeServer = new IpcServer(PipeNameSuffix, HandlePipeMessage);
		}

		/// <summary>updater 临时启动目录（测试断言复制产物用）。</summary>
		internal string TempLaunchDir => _tempLaunchDir;

		/// <summary>
		/// 复制 updater 到临时目录并启动。zipUrl 为平台包直链。
		/// 返回 false = updater 产物缺失（安装目录里没有 ForkPlus.AutoUpdater，
		/// 调用方回退浏览器打开）。
		/// </summary>
		public bool Start(string zipUrl)
		{
			if (_disposed)
			{
				throw new ObjectDisposedException(nameof(AutoUpdateRunner));
			}
			string updaterSourcePath = Path.Combine(_updaterExeSourceDir, Consts.ForkPlus.AutoUpdaterFilename);
			if (!File.Exists(updaterSourcePath))
			{
				Log.Info("AutoUpdater helper not found at " + updaterSourcePath + "; falling back to browser download");
				return false;
			}
			CopyUpdaterToTempDir(updaterSourcePath);
			string arguments = BuildArguments(zipUrl);
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = Path.Combine(_tempLaunchDir, Consts.ForkPlus.AutoUpdaterFilename),
				Arguments = arguments,
				UseShellExecute = false,
				CreateNoWindow = true,
				WorkingDirectory = _tempLaunchDir
			};
			_updaterProcess = Process.Start(startInfo);
			IsDownloading = true;
			// 进程退出看门狗：管道从未连上（连接超时/降级）时 Exited 仍需可靠触发
			System.Threading.Tasks.Task.Run(delegate
			{
				try
				{
					_updaterProcess.WaitForExit();
				}
				catch (Exception)
				{
				}
				RaiseExited();
			});
			return true;
		}

		/// <summary>构造 updater 命令行（internal 供测试断言参数口径）。</summary>
		internal string BuildArguments(string zipUrl)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("--url ").Append(Quote(zipUrl));
			sb.Append(" --install-dir ").Append(Quote(_installDir));
			sb.Append(" --restart-command ").Append(Quote(_restartCommand ?? ""));
			sb.Append(" --pipe-pid ").Append(App.ProcessId);
			sb.Append(" --wait-pid ").Append(_waitPid);
			if (_noRestart)
			{
				sb.Append(" --no-restart");
			}
			if (_resetSettings)
			{
				// "重置此版本"流：updater 在文件替换成功后删除设置文件（失败路径不动）
				sb.Append(" --reset-settings");
				if (!string.IsNullOrEmpty(_settingsFile))
				{
					sb.Append(" --settings-file ").Append(Quote(_settingsFile));
				}
			}
			return sb.ToString();
		}

		/// <summary>
		/// 取消更新：Kill updater 进程。仅下载阶段（IsDownloading=true）实际执行——
		/// 解压阶段文件全在临时区、安装目录未动，Kill 无害；但 waiting-exit 之后的
		/// replacing 阶段 Kill 会把安装目录留在半新半旧态，必须禁止。窗口在
		/// waiting-exit 收到时已触发应用关闭，此后 Cancel 只做状态复位。幂等。
		/// </summary>
		public void Cancel()
		{
			if (!IsDownloading)
			{
				return;
			}
			Process process = _updaterProcess;
			if (process != null && !process.HasExited)
			{
				try
				{
					process.Kill(entireProcessTree: true);
				}
				catch (Exception ex)
				{
					Log.Warn("Failed to kill AutoUpdater process", ex);
				}
			}
			IsDownloading = false;
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;
			Cancel();
			try
			{
				_pipeServer.Dispose();
			}
			catch (Exception)
			{
			}
			TryDeleteDir(_tempLaunchDir);
		}

		/// <summary>管道消息入口（IpcServer 线程）：解析 + 转发到 UI 线程事件。</summary>
		private void HandlePipeMessage(NamedPipeServerStream stream)
		{
			try
			{
				string message;
				while ((message = stream.ReadString()) != null)
				{
					AutoUpdateProgress progress = AutoUpdateProgress.Parse(message);
					if (progress == null)
					{
						continue;
					}
					if (progress.Phase != null && progress.Phase != "downloading")
					{
						IsDownloading = false;
					}
					RaiseProgress(progress);
				}
			}
			catch (Exception ex)
			{
				// updater 被杀/退出导致管道断开是正常终态（IOException/EndOfStream）
				Log.Debug("Update progress pipe closed: " + ex.Message);
			}
			RaiseExited();
		}

		private void RaiseProgress(AutoUpdateProgress progress)
		{
			Post(delegate
			{
				Progress?.Invoke(progress);
			});
		}

		private void RaiseExited()
		{
			// 管道断开与看门狗双路径幂等（Interlocked 保证只发一次）
			if (System.Threading.Interlocked.Exchange(ref _exitRaised, 1) == 1)
			{
				return;
			}
			IsDownloading = false;
			Post(delegate
			{
				Exited?.Invoke();
			});
		}

		private static void Post(Action action)
		{
			if (global::Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
			{
				action();
			}
			else
			{
				global::Avalonia.Threading.Dispatcher.UIThread.Post(action);
			}
		}

		private void CopyUpdaterToTempDir(string updaterSourcePath)
		{
			Directory.CreateDirectory(_tempLaunchDir);
			// apphost 名按平台（Windows 带扩展 .exe，Unix 无扩展），托管件名跨平台一致
			string[] filesToCopy = new string[4]
			{
				Consts.ForkPlus.AutoUpdaterFilename,
				"ForkPlus.AutoUpdater.dll",
				"ForkPlus.AutoUpdater.runtimeconfig.json",
				"ForkPlus.AutoUpdater.deps.json"
			};
			foreach (string fileName in filesToCopy)
			{
				string source = Path.Combine(_updaterExeSourceDir, fileName);
				if (File.Exists(source))
				{
					File.Copy(source, Path.Combine(_tempLaunchDir, fileName), overwrite: true);
				}
			}
			// 自包含发布（生产产物，无 .NET 运行时机器）下，updater 的 apphost 启动时必须在
			// 自身同一目录找到 hostfxr / hostpolicy / coreclr，否则进程立刻以错误退出——
			// 表现为"点击重置此版本后 updater 没跑起来、窗口静默回退到'已是最新版本'"。
			// 这里把安装目录里自包含运行时组件一并复制（安装目录与主程序同一自包含运行时，
			// 一定存在；框架依赖构建/测试输出里没有则跳过，行为不变）。
			CopyIfPresent("hostfxr.dll", "libhostfxr.so", "libhostfxr.dylib");
			CopyIfPresent("hostpolicy.dll", "libhostpolicy.so", "libhostpolicy.dylib");
			CopyIfPresent("coreclr.dll", "libcoreclr.so", "libcoreclr.dylib");
		}

		/// <summary>把安装目录里的宿主运行时文件复制到临时启动目录（按当前平台取一个候选名，存在才拷）。</summary>
		private void CopyIfPresent(params string[] candidates)
		{
			foreach (string fileName in candidates)
			{
				string source = Path.Combine(_updaterExeSourceDir, fileName);
				if (File.Exists(source))
				{
					File.Copy(source, Path.Combine(_tempLaunchDir, fileName), overwrite: true);
					return;
				}
			}
		}

		/// <summary>命令行引号（含空格路径安全；updater 侧按整段读值不解析引号内容）。</summary>
		private static string Quote(string value)
		{
			return "\"" + (value ?? "").Replace("\"", "'") + "\"";
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
				// 残留下次启动由 updater 的 CleanupStaleWorkDirs 收
			}
		}
	}
}
